using NordvikManager.Application.Commands.Player.BanUser;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.Commands.Player.GetLocalPlayers;
using NordvikManager.Application.Commands.Player.KickPlayer;
using NordvikManager.Application.Commands.Player.RemoveUserPlayers;
using NordvikManager.Application.Commands.Player.UnbanUser;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/User (UserController delegates here).</summary>
    public sealed class UserEndpoints
    {
        private readonly ICentralServerService _centralServer;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly ILobbyService _lobbyService;

        public UserEndpoints(ICentralServerService centralServer, IMediator mediator, IConfiguration configuration, ILobbyService lobbyService)
        {
            _centralServer = centralServer;
            _mediator = mediator;
            _configuration = configuration;
            _lobbyService = lobbyService;
            _configuration = configuration;
        }

        [ApiRoute("POST", "api/User/Login")]
        public async Task<ApiResult> Login(ApiContext context, Models.LoginRequest loginData)
        {
            var centralResult = await _centralServer.LoginAsync(loginData.Username, loginData.Password);
            if (centralResult == null)
                return ApiResult.Unauthorized("Wrong credentials");

            var isBanned = await _mediator.Send(new CheckUserBannedCommand { CentralUserId = centralResult.UserId });
            if (isBanned)
                return ApiResult.Unauthorized("User is banned from this server");

            // Issue a LOCAL JWT signed with this GM Backend's own secret.
            // The Central Server's JWT_SECRET is never used or stored here.
            var localToken = IssueLocalToken(centralResult);

            // Local JWT — used by the GM Admin Frontend for all GM Backend requests.
            context.SetCookie("Authorization", localToken);

            // Central Server access token (15 min JWT) — forwarded server-side to Central Server
            // for proxied calls (invites, keyboard bindings, session creation, etc.).
            context.SetCookie("CentralToken", centralResult.CentralToken);

            // Central Server refresh token — used to silently obtain a new CentralToken
            // when the 15-min access token expires, without requiring re-login.
            if (!string.IsNullOrEmpty(centralResult.RefreshToken))
                context.SetCookie("CentralRefreshToken", centralResult.RefreshToken);

            return ApiResult.Ok(new { token = localToken });
        }

        [ApiRoute("GET", "api/User/CheckLogin")]
        public ApiResult CheckLogin(ApiContext context) => ApiResult.Ok();

        [ApiRoute("GET", "api/User/Logout")]
        public ApiResult Logout(ApiContext context)
        {
            context.DeleteCookie("Authorization");
            context.DeleteCookie("CentralToken");
            context.DeleteCookie("CentralRefreshToken");
            return ApiResult.Ok();
        }

        /// <summary>
        /// Silently refreshes the CentralToken using the stored CentralRefreshToken.
        /// Called by the frontend or other endpoints when the 15-min access token has expired.
        /// Returns 200 with the new token on success, 401 if the refresh token is missing/expired.
        /// The GM's browser uses the returned access token for signaling (it has no Central
        /// cookie of its own), as it does the one StartSession returns.
        /// </summary>
        [ApiRoute("POST", "api/User/refresh-central")]
        public async Task<ApiResult> RefreshCentralToken(ApiContext context)
        {
            var refreshToken = context.ReadCookie("CentralRefreshToken");
            if (string.IsNullOrEmpty(refreshToken))
                return ApiResult.Unauthorized(new { error = "No Central Server refresh token. Please log in again." });

            var tokens = await _centralServer.RefreshTokenAsync(refreshToken);
            if (tokens == null)
                return ApiResult.Unauthorized(new { error = "Central Server refresh failed. Please log in again." });

            context.Refreshed(tokens);

            return ApiResult.Ok(new { refreshed = true, accessToken = tokens.AccessToken });
        }

        [ApiRoute("GET", "api/User/UserInfo")]
        public ApiResult GetUserInfo(ApiContext context)
        {
            var user = context.User;
            return ApiResult.Ok(new
            {
                Admin = user?.IsAdmin,
                LocalAdmin = user?.IsLocalAdmin,
                UserName = user?.UserName,
                Email = user?.Email
            });
        }

        [ApiRoute("GET", "api/User/GetUserNameById")]
        public async Task<ApiResult> GetUserNameById(ApiContext context, string id)
        {
            var centralToken = CentralToken(context);
            var userName = await _centralServer.GetUserNameAsync(centralToken, id);
            if (userName == null) return ApiResult.NotFound();
            return ApiResult.Ok(new { userName });
        }


        [ApiRoute("POST", "api/User/Register")]
        public async Task<ApiResult> Register(ApiContext context, Models.RegisterRequest registerRequest)
        {
            var (success, message) = await _centralServer.RegisterAsync(
                registerRequest.Username,
                registerRequest.Email,
                registerRequest.Password,
                registerRequest.InviteCode);

            if (success) return ApiResult.Ok(new { message });
            return ApiResult.BadRequest(new { message });
        }

        [ApiRoute("GET", "api/User/CheckRegistrationKey")]
        public async Task<ApiResult> CheckRegistrationKey(ApiContext context, string key)
        {
            var valid = await _centralServer.CheckRegistrationKeyAsync(key);
            if (valid) return ApiResult.Ok(new { result = "ok" });
            return ApiResult.BadRequest();
        }

        [ApiRoute("GET", "api/User/users")]
        public async Task<ApiResult> Users(ApiContext context, int page = 1, int count = 10)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            if (page < 1 || count < 1) return ApiResult.BadRequest();

            var result = await _mediator.Send(new GetLocalPlayersCommand { Page = page, Count = count });
            return ApiResult.Ok(result);
        }

        [ApiRoute("DELETE", "api/User/DeleteInvite")]
        public async Task<ApiResult> DeleteInvite(ApiContext context, string key)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            var success = await _centralServer.DeleteInviteAsync(CentralToken(context), key);
            if (success) return ApiResult.Ok(new { result = "ok" });
            return ApiResult.NotFound();
        }

        [ApiRoute("GET", "api/User/KeyboardBindings")]
        public async Task<ApiResult> KeyboardBindings(ApiContext context)
        {
            var bindings = await _centralServer.GetKeyboardBindingsAsync(CentralToken(context));
            if (bindings == null) return ApiResult.Status(502, "Central server unavailable");
            return ApiResult.Ok(bindings);
        }

        // Key order must match the frontend's CreateActionName emission order
        // (Ctrl, Shift, Alt) — Ctrl+Alt+Shift ordering here would silently
        // reject triple-modifier combos. Anchored at both ends: the previous
        // regex had no leading '^', so it matched any string merely ending in
        // a valid-looking combo rather than requiring the whole string to be one.
        private static readonly System.Text.RegularExpressions.Regex KeyboardBindingKeyRegex = new(
            @"^(Ctrl\+)?(Shift\+)?(Alt\+)?(.|HOME|DELETE|INSERT|PAGEUP|END|PAGEDOWN|BACKSPACE)$");
        // "panel.command" optionally followed by whitespace and an argument tail
        // (e.g. "Playlist.PlaySound --resourceId=abc-123") — a ClientMediator
        // command reference the frontend parses and dispatches. Empty string is a
        // valid tombstone value (unbinds a built-in default's key client-side).
        // Must stay in sync with COMMAND_REGEX (frontend KeyBoardEventsManager.js)
        // and KEYBOARD_BINDING_COMMAND_REGEX (Central routes/user.js).
        private static readonly System.Text.RegularExpressions.Regex KeyboardBindingCommandRegex = new(
            @"^[A-Za-z_][\w-]*\.[A-Za-z_][\w-]*(\s+\S.*)?$");
        private const int MaxKeyboardBindings = 200;
        // Room for a command plus a GUID-valued argument or two.
        private const int MaxKeyboardBindingStringLength = 256;

        [ApiRoute("POST", "api/User/KeyboardBindings")]
        public async Task<ApiResult> SaveKeyboardBindings(ApiContext context, Dictionary<string, string> bindings)
        {
            if (bindings.Count > MaxKeyboardBindings)
                return ApiResult.BadRequest();

            foreach (var (key, value) in bindings)
            {
                if (key.Length > MaxKeyboardBindingStringLength || !KeyboardBindingKeyRegex.IsMatch(key))
                    return ApiResult.BadRequest();

                if (value == null || value.Length > MaxKeyboardBindingStringLength)
                    return ApiResult.BadRequest();

                if (value != "" && !KeyboardBindingCommandRegex.IsMatch(value))
                    return ApiResult.BadRequest();
            }

            var success = await _centralServer.SetKeyboardBindingsAsync(CentralToken(context), bindings);
            if (success) return ApiResult.Ok();
            return ApiResult.BadRequest();
        }

        [ApiRoute("GET", "api/User/getplayers")]
        public async Task<ApiResult> GetPlayers(ApiContext context, int page = 1, int count = 10)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            if (page < 1 || count < 1) return ApiResult.BadRequest();

            var result = await _mediator.Send(new GetLocalPlayersCommand { Page = page, Count = count });
            return ApiResult.Ok(result);
        }

        [ApiRoute("POST", "api/User/kickplayer")]
        public async Task<ApiResult> KickPlayer(ApiContext context, Models.KickPlayerRequest request)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            await _mediator.Send(new KickPlayerCommand { PlayerId = request.PlayerId });

            _lobbyService.SendKickToPlayer(request.PlayerId);
            return ApiResult.Ok();
        }

        [ApiRoute("POST", "api/User/removeuser")]
        public async Task<ApiResult> RemoveUser(ApiContext context, Models.CentralUserRequest request)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            await _mediator.Send(new RemoveUserPlayersCommand { CentralUserId = request.CentralUserId });
            return ApiResult.Ok();
        }

        [ApiRoute("POST", "api/User/banuser")]
        public async Task<ApiResult> BanUser(ApiContext context, Models.CentralUserRequest request, [ApiService] IWebRTCSessionService webRtcSession)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            var result = await _mediator.Send(new BanUserCommand { CentralUserId = request.CentralUserId });
            if (result != NordvikManager.Domain.Enums.CommandResponse.Ok) return ApiResult.BadRequest();

            // The ban applies now: the user's open game connections are closed, not only refused next time.
            await webRtcSession.DisconnectUserAsync(request.CentralUserId);
            return ApiResult.Ok();
        }

        [ApiRoute("POST", "api/User/unbanuser")]
        public async Task<ApiResult> UnbanUser(ApiContext context, Models.CentralUserRequest request)
        {
            var user = context.User;
            if (user?.IsLocalAdmin != true) return ApiResult.Unauthorized();

            await _mediator.Send(new UnbanUserCommand { CentralUserId = request.CentralUserId });
            return ApiResult.Ok();
        }

        // -------------------------------------------------------------------------

        private static string CentralToken(ApiContext context) =>
            context.ReadCookie("CentralToken") ?? string.Empty;

        private string IssueLocalToken(CentralLoginResult centralResult)
        {
            var secret = _configuration["JWTSecret"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var expireHours = _configuration.GetValue<int>("JWT:ExpireHours", 3);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, centralResult.UserId),
                new Claim("username", centralResult.UserName ?? string.Empty),
                new Claim("email",    centralResult.Email    ?? string.Empty),
                new Claim("isAdmin",  centralResult.IsAdmin.ToString().ToLowerInvariant()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expireHours),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}

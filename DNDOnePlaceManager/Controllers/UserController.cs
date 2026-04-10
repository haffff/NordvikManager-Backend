using DndOnePlaceManager.Application.Commands.Player.BanUser;
using DndOnePlaceManager.Application.Commands.Player.CheckUserBanned;
using DndOnePlaceManager.Application.Commands.Player.GetLocalPlayers;
using DndOnePlaceManager.Application.Commands.Player.KickPlayer;
using DndOnePlaceManager.Application.Commands.Player.RemoveUserPlayers;
using DndOnePlaceManager.Application.Commands.Player.UnbanUser;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly ICentralServerService _centralServer;
        private readonly IMediator _mediator;
        private readonly IConfiguration _configuration;
        private readonly ILobbyService _lobbyService;

        public UserController(ICentralServerService centralServer, IMediator mediator, IConfiguration configuration, ILobbyService lobbyService)
        {
            _centralServer = centralServer;
            _mediator = mediator;
            _configuration = configuration;
            _lobbyService = lobbyService;
            _configuration = configuration;
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] Models.LoginRequest loginData)
        {
            var centralResult = await _centralServer.LoginAsync(loginData.Username, loginData.Password);
            if (centralResult == null)
                return Unauthorized("Wrong credentials");

            var isBanned = await _mediator.Send(new CheckUserBannedCommand { CentralUserId = centralResult.UserId });
            if (isBanned)
                return Unauthorized("User is banned from this server");

            // Issue a LOCAL JWT signed with this GM Backend's own secret.
            // The Central Server's JWT_SECRET is never used or stored here.
            var localToken = IssueLocalToken(centralResult);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            };

            // Local JWT — used by the GM Admin Frontend for all GM Backend requests.
            HttpContext.Response.Cookies.Append("Authorization", localToken, cookieOptions);

            // Central Server token — stored server-side via a separate HttpOnly cookie so
            // proxied calls (invites, keyboard bindings, etc.) can forward it to the Central Server.
            HttpContext.Response.Cookies.Append("CentralToken", centralResult.CentralToken, cookieOptions);

            return Ok(new { token = localToken });
        }

        [Authorize]
        [HttpGet]
        [Route("CheckLogin")]
        public IActionResult CheckLogin() => Ok();

        [Authorize]
        [HttpGet]
        [Route("Logout")]
        public IActionResult Logout()
        {
            HttpContext.Response.Cookies.Delete("Authorization");
            HttpContext.Response.Cookies.Delete("CentralToken");
            return Ok();
        }

        [Authorize]
        [HttpGet]
        [Route("UserInfo")]
        public IActionResult GetUserInfo()
        {
            var user = HttpContext.Items["User"] as User;
            return Ok(new
            {
                Admin = user?.IsAdmin,
                LocalAdmin = user?.IsLocalAdmin,
                UserName = user?.UserName,
                Email = user?.Email
            });
        }

        [Authorize]
        [HttpGet]
        [Route("GetUserNameById")]
        public async Task<IActionResult> GetUserNameById(string id)
        {
            var centralToken = CentralToken();
            var userName = await _centralServer.GetUserNameAsync(centralToken, id);
            if (userName == null) return NotFound();
            return Ok(new { userName });
        }


        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> Register([FromBody] Models.RegisterRequest registerRequest)
        {
            var (success, message) = await _centralServer.RegisterAsync(
                registerRequest.Username,
                registerRequest.Email,
                registerRequest.Password,
                registerRequest.InviteCode);

            if (success) return Ok(new { message });
            return BadRequest(new { message });
        }

        [HttpGet]
        [Route("CheckRegistrationKey")]
        public async Task<IActionResult> CheckRegistrationKey(string key)
        {
            var valid = await _centralServer.CheckRegistrationKeyAsync(key);
            if (valid) return Ok(new { result = "ok" });
            return BadRequest();
        }

        [HttpGet]
        [Authorize]
        [Route("users")]
        public async Task<IActionResult> Users(int page = 1, int count = 10)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            if (page < 1 || count < 1) return BadRequest();

            var result = await _mediator.Send(new GetLocalPlayersCommand { Page = page, Count = count });
            return Ok(result);
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteInvite")]
        public async Task<IActionResult> DeleteInvite([FromQuery] string key)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            var success = await _centralServer.DeleteInviteAsync(CentralToken(), key);
            if (success) return Ok(new { result = "ok" });
            return NotFound();
        }

        [HttpGet]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> KeyboardBindings()
        {
            var bindings = await _centralServer.GetKeyboardBindingsAsync(CentralToken());
            if (bindings == null) return StatusCode(502, "Central server unavailable");
            return Ok(bindings);
        }

        [HttpPost]
        [Authorize]
        [Route("KeyboardBindings")]
        public async Task<IActionResult> SaveKeyboardBindings([FromBody] Dictionary<string, string> bindings)
        {
            var regex = new System.Text.RegularExpressions.Regex(
                @"(Ctrl\+)*(Alt\+)*(Shift\+)*(.|HOME|DELETE|INSERT|PAGEUP|END|PAGEDOWN|BACKSPACE)$");

            foreach (var key in bindings.Keys)
            {
                if (!regex.IsMatch(key))
                    return BadRequest();
            }

            var success = await _centralServer.SetKeyboardBindingsAsync(CentralToken(), bindings);
            if (success) return Ok();
            return BadRequest();
        }

        [HttpGet]
        [Authorize]
        [Route("getplayers")]
        public async Task<IActionResult> GetPlayers(int page = 1, int count = 10)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            if (page < 1 || count < 1) return BadRequest();

            var result = await _mediator.Send(new GetLocalPlayersCommand { Page = page, Count = count });
            return Ok(result);
        }

        [HttpPost]
        [Authorize]
        [Route("kickplayer")]
        public async Task<IActionResult> KickPlayer([FromBody] Models.KickPlayerRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            var result = await _mediator.Send(new KickPlayerCommand { PlayerId = request.PlayerId });
            if (result != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok) return BadRequest();

            _lobbyService.SendKickToPlayer(request.PlayerId);
            return Ok();
        }

        [HttpPost]
        [Authorize]
        [Route("removeuser")]
        public async Task<IActionResult> RemoveUser([FromBody] Models.CentralUserRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            var result = await _mediator.Send(new RemoveUserPlayersCommand { CentralUserId = request.CentralUserId });
            if (result == DndOnePlaceManager.Domain.Enums.CommandResponse.Ok) return Ok();
            return BadRequest();
        }

        [HttpPost]
        [Authorize]
        [Route("banuser")]
        public async Task<IActionResult> BanUser([FromBody] Models.CentralUserRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            var result = await _mediator.Send(new BanUserCommand { CentralUserId = request.CentralUserId });
            if (result == DndOnePlaceManager.Domain.Enums.CommandResponse.Ok) return Ok();
            return BadRequest();
        }

        [HttpPost]
        [Authorize]
        [Route("unbanuser")]
        public async Task<IActionResult> UnbanUser([FromBody] Models.CentralUserRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            if (!user.IsLocalAdmin) return Unauthorized();

            var result = await _mediator.Send(new UnbanUserCommand { CentralUserId = request.CentralUserId });
            if (result == DndOnePlaceManager.Domain.Enums.CommandResponse.Ok) return Ok();
            return BadRequest();
        }

        // -------------------------------------------------------------------------

        private string CentralToken() =>
            HttpContext.Request.Cookies["CentralToken"] ?? string.Empty;

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

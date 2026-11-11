using NordvikManager.Services;
using NordvikManager.Application.Commands.Game.GameExists;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Session (SessionController delegates here).</summary>
    public sealed class SessionEndpoints
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;
        private readonly IWebRTCSessionService _webRTCSession;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ICentralServerService _centralServerService;

        public SessionEndpoints(
            IMediator mediator,
            ILobbyRegistry lobbyRegistry,
            IWebRTCSessionService webRTCSession,
            IServiceScopeFactory serviceScopeFactory,
            ICentralServerService centralServerService)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
            _webRTCSession = webRTCSession;
            _serviceScopeFactory = serviceScopeFactory;
            _centralServerService = centralServerService;
        }

        /// <summary>
        /// Starts a WebRTC session for the given game.
        /// Creates the GameLobby and connects to Central Server signaling so peers can join.
        /// Safe to call multiple times — returns early if the session is already active.
        /// If no Central Server session exists for this game, one is created automatically.
        /// </summary>
        [ApiRoute("POST", "api/Session/{gameId}/start")]
        public async Task<ApiResult> StartSession(ApiContext context, Guid gameId)
        {
            try
            {
                return await StartSessionCore(context, gameId);
            }
            catch (SignalingAuthException ex)
            {
                // Central Server refused this backend (e.g. outdated protocol version).
                // The GM UI shows "error" as-is.
                return ApiResult.Conflict(new { error = ex.Message });
            }
        }

        private async Task<ApiResult> StartSessionCore(ApiContext context, Guid gameId)
        {
            var user = context.User;

            // 401 only for a missing login: the client sends the user to log in on 401,
            // then retries the same game — a missing game or a non-member answered 401
            // too, which made that an endless login loop.
            if (user == null)
                return ApiResult.Unauthorized(new { error = "Not logged in." });

            if (!await _mediator.Send(new GameExistsCommand { GameId = gameId }))
                return ApiResult.NotFound(new { error = "This game no longer exists." });

            var playerResponse = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (playerResponse.Player == null)
                return ApiResult.Status(403, new { error = "You are not a player in this game." });

            var centralToken = await GetOrRefreshCentralTokenAsync(context);
            if (string.IsNullOrEmpty(centralToken))
                return ApiResult.BadRequest(new { error = "No CentralToken cookie. Please log in to the Central Server first." });

            // The game's Central session: made when there's none (a game created while the token
            // was expired) or when Central no longer has it (expired, deleted); otherwise its
            // listing — cover image, password — is re-sent.
            // Making it makes its owner on Central, who may then connect as the game's GM: only
            // the GM (or the server's local admin) may, not any player starting the game.
            var mayCreate = playerResponse.Player.IsOwner == true || user.IsLocalAdmin;
            var central = await new CentralSessionSync(_mediator, _centralServerService).EnsureAsync(gameId, centralToken, mayCreate);
            if (central.Details == null)
                return ApiResult.NotFound(new { error = "Game not found." });
            if (central.NeedsOwner)
                return ApiResult.Conflict(new { error = "This game has no session on the Central Server right now. Ask its GM to start it." });
            var centralSessionId = central.SessionId;
            if (centralSessionId == null)
                return ApiResult.Status(502, new { error = "Could not create Central Server session. The Central Server may be unreachable." });

            // Central signaling only lets the owner and the session's members connect as players,
            // and someone who joined this game here (not through the player client) isn't one yet.
            // Best effort: joining again is a no-op, and Central answers ok for the owner.
            await _centralServerService.JoinSessionAsync(centralToken, centralSessionId, central.Details.Password);

            // Lobby already exists — check if WebRTC signaling is still connected.
            if (_lobbyRegistry.Games.ContainsKey(gameId))
            {
                if (_webRTCSession.IsConnected(gameId))
                    return ApiResult.Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = true });

                // Lobby is registered but signaling dropped — reconnect without recreating the lobby.
                await _webRTCSession.StartSessionAsync(gameId, centralSessionId, centralToken);
                return ApiResult.Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = false });
            }

            // Pre-create the lobby so SystemPlayer is set before any WebRTC peer connects.
            // Mirrors the GetOrAdd in the old WebSocketManager.HandleLobbyJoining.
            var systemPlayer = await _mediator.Send(new GetSystemPlayerCommand { GameID = gameId });
            _lobbyRegistry.Games.GetOrAdd(gameId, _ => new GameLobby(_serviceScopeFactory)
            {
                GameId = gameId,
                SystemPlayer = systemPlayer
            });

            await _webRTCSession.StartSessionAsync(gameId, centralSessionId, centralToken);

            return ApiResult.Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = false });
        }

        /// <summary>
        /// Stops the WebRTC session and removes the lobby for the given game.
        /// Only the game owner may call this.
        /// </summary>
        [ApiRoute("POST", "api/Session/{gameId}/stop")]
        public async Task<ApiResult> StopSession(ApiContext context, Guid gameId)
        {
            var user = context.User;

            var playerResponse = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (playerResponse.Player?.IsOwner != true)
                return ApiResult.Forbid();

            await _webRTCSession.StopSessionAsync(gameId);
            _lobbyRegistry.Games.TryRemove(gameId, out _);

            return ApiResult.Ok();
        }

        /// <summary>
        /// Returns the backend's own Game ID for the connection this request arrived on.
        ///
        /// A player who joins via an invite link only ever learns the Central Server's
        /// session ID (see GameListItem.js's "?game=" link and Central's gamelist/join,
        /// which never returns this backend's own Games.Id) — a different ID space
        /// entirely. WebRTCInProcessDispatcher already corrects this transparently for
        /// REST calls tunneled through the data channel (it overrides "gameId" in the
        /// query string with the connection's authoritative value), but raw WebSocket
        /// Send() commands (e.g. property_add) have no equivalent correction, since the
        /// property system is generic and a parentId legitimately isn't always "the
        /// current game". The frontend calls this once after connecting to learn and
        /// correct its own locally-held game id before anything depends on it.
        ///
        /// Returns 404 outside a WebRTC-tunneled request — the GM's direct HTTP calls
        /// never need this, since the GM always already holds the real Games.Id.
        /// </summary>
        [ApiRoute("GET", "api/Session/whoami")]
        public ApiResult WhoAmI(ApiContext context)
        {
            if (context.TunnelGameId is Guid gameId)
                return ApiResult.Ok(new { gameId });

            return ApiResult.NotFound();
        }

        /// <summary>
        /// Returns the current CentralToken if it is still valid (more than 1 minute left).
        /// If expired, tries to refresh it using the stored CentralRefreshToken and
        /// updates the CentralToken cookie before returning the new value.
        /// Returns null if no valid token can be obtained.
        /// </summary>
        private async Task<string?> GetOrRefreshCentralTokenAsync(ApiContext context)
        {
            var token = context.ReadCookie("CentralToken");

            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    if (jwt.ValidTo > DateTime.UtcNow.AddMinutes(1))
                        return token;
                }
            }

            // Token is missing or expired — try to refresh.
            var refreshToken = context.ReadCookie("CentralRefreshToken");
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var tokens = await _centralServerService.RefreshTokenAsync(refreshToken);
            if (tokens == null)
                return null;

            context.Refreshed(tokens);
            return tokens.AccessToken;
        }
    }

}

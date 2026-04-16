using DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId;
using DndOnePlaceManager.Application.Commands.Game.GetGameSessionDetails;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Game.SetGameCentralSession;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SessionController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;
        private readonly IWebRTCSessionService _webRTCSession;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ICentralServerService _centralServerService;

        public SessionController(
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
        [HttpPost("{gameId}/start")]
        public async Task<IActionResult> StartSession(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResponse = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (playerResponse.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            var centralToken = await GetOrRefreshCentralTokenAsync();
            if (string.IsNullOrEmpty(centralToken))
                return BadRequest(new { error = "No CentralToken cookie. Please log in to the Central Server first." });

            var centralSessionId = await _mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId });

            // No Central Server session — create one now (handles games created when token was expired).
            if (centralSessionId == null)
            {
                var details = await _mediator.Send(new GetGameSessionDetailsCommand { GameId = gameId });
                if (details == null)
                    return NotFound(new { error = "Game not found." });

                centralSessionId = await _centralServerService.CreateSessionAsync(
                    centralToken,
                    new GameItemDTO
                    {
                        Name = details.Name,
                        ShortDescription = details.Summary,
                        LongDescription = details.Description,
                        PasswordRequired = details.PasswordRequired,
                        IsPublic = details.IsPublic
                    });

                if (centralSessionId == null)
                    return StatusCode(502, new { error = "Could not create Central Server session. The Central Server may be unreachable." });

                await _mediator.Send(new SetGameCentralSessionCommand
                {
                    GameId = gameId,
                    CentralSessionId = centralSessionId
                });
            }

            // Lobby already exists — check if WebRTC signaling is still connected.
            if (_lobbyRegistry.Games.ContainsKey(gameId))
            {
                if (_webRTCSession.IsConnected(gameId))
                    return Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = true });

                // Lobby is registered but signaling dropped — reconnect without recreating the lobby.
                await _webRTCSession.StartSessionAsync(gameId, centralSessionId, centralToken);
                return Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = false });
            }

            // Pre-create the lobby so SystemPlayer is set before any WebRTC peer connects.
            // Mirrors the GetOrAdd in the old WebSocketManager.HandleLobbyJoining.
            var systemPlayer = await _mediator.Send(new GetSystemPlayerCommand { GameID = gameId });
            _lobbyRegistry.Games.GetOrAdd(gameId, _ => new GameLobby(_serviceScopeFactory)
            {
                GameId = gameId,
                SystemPlayer = systemPlayer,
                ConnectedPlayers = new Dictionary<PlayerDTO, List<IPlayerConnection>>()
            });

            await _webRTCSession.StartSessionAsync(gameId, centralSessionId, centralToken);

            return Ok(new { centralSessionId, centralAccessToken = centralToken, alreadyActive = false });
        }

        /// <summary>
        /// Stops the WebRTC session and removes the lobby for the given game.
        /// Only the game owner may call this.
        /// </summary>
        [HttpPost("{gameId}/stop")]
        public async Task<IActionResult> StopSession(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResponse = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (playerResponse.Player?.IsOwner != true)
                return Forbid();

            await _webRTCSession.StopSessionAsync(gameId);
            _lobbyRegistry.Games.TryRemove(gameId, out _);

            return Ok();
        }

        /// <summary>
        /// Returns the current CentralToken if it is still valid (more than 1 minute left).
        /// If expired, tries to refresh it using the stored CentralRefreshToken and
        /// updates the CentralToken cookie before returning the new value.
        /// Returns null if no valid token can be obtained.
        /// </summary>
        private async Task<string?> GetOrRefreshCentralTokenAsync()
        {
            var token = Request.Cookies["CentralToken"];

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
            var refreshToken = Request.Cookies["CentralRefreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return null;

            var newToken = await _centralServerService.RefreshTokenAsync(refreshToken);
            if (newToken == null)
                return null;

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            };
            Response.Cookies.Append("CentralToken", newToken, cookieOptions);

            return newToken;
        }
    }
}

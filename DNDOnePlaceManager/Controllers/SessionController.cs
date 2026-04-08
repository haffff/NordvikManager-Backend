using DndOnePlaceManager.Application.Commands.Game.GetGameCentralSessionId;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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

        public SessionController(
            IMediator mediator,
            ILobbyRegistry lobbyRegistry,
            IWebRTCSessionService webRTCSession,
            IServiceScopeFactory serviceScopeFactory)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
            _webRTCSession = webRTCSession;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Starts a WebRTC session for the given game.
        /// Creates the GameLobby and connects to Central Server signaling so peers can join.
        /// Safe to call multiple times — returns early if the session is already active.
        /// </summary>
        [HttpPost("{gameId}/start")]
        public async Task<IActionResult> StartSession(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResponse = await _mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
            if (playerResponse.Player == null)
                return Unauthorized(new { error = "You are not a player in this game." });

            var centralToken = Request.Cookies["CentralToken"];
            if (string.IsNullOrEmpty(centralToken))
                return BadRequest(new { error = "No CentralToken cookie. Please log in to the Central Server first." });

            var centralSessionId = await _mediator.Send(new GetGameCentralSessionIdCommand { GameID = gameId });
            if (centralSessionId == null)
                return NotFound(new { error = "No Central Server session is associated with this game." });

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
    }
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Game.GameExists;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.Services;
using NordvikManager.WebRTC;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="SessionEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SessionController : Controller
    {
        private readonly SessionEndpoints endpoints;

        public SessionController(
            IMediator mediator,
            ILobbyRegistry lobbyRegistry,
            IWebRTCSessionService webRTCSession,
            IServiceScopeFactory serviceScopeFactory,
            ICentralServerService centralServerService)
        {
            endpoints = new SessionEndpoints(mediator, lobbyRegistry, webRTCSession, serviceScopeFactory, centralServerService);
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
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.StartSession(context, gameId));
        }

        /// <summary>
        /// Stops the WebRTC session and removes the lobby for the given game.
        /// Only the game owner may call this.
        /// </summary>
        [HttpPost("{gameId}/stop")]
        public async Task<IActionResult> StopSession(Guid gameId)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.StopSession(context, gameId));
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
        [HttpGet("whoami")]
        public IActionResult WhoAmI()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, endpoints.WhoAmI(context));
        }
    }
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NordvikManager.Api.Endpoints;
using NordvikManager.Application.Commands.Addons.GetAddonsFromRepository;
using NordvikManager.Application.Commands.Application;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.DeleteGame;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.Commands.Game.UpdateGame;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.Commands.Properties.GetPropertiesByQuery;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.Services;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Infrastructure.Services.Settings;
using NordvikManager.Services.Interfaces;
using NordvikManager.Services;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>The logic is in <see cref="GameListEndpoints"/>, shared with hosts without ASP.NET Core.</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class GameListController : Controller
    {
        private readonly GameListEndpoints endpoints;

        public GameListController(IMediator mediator, IConfiguration configuration, ILobbyService lobbyService, ICentralServerService centralServerService, IWebRTCSessionService webRtcSessionService, IHttpClientFactory httpClientFactory, ILogger<GameListController> logger)
        {
            endpoints = new GameListEndpoints(mediator, configuration, lobbyService, centralServerService, webRtcSessionService, httpClientFactory, logger);
        }

        /// <summary>
        /// Proxies GET /api/gamelist/publicgames → Central Server GET /api/gamelist/publicgames.
        /// Exists to avoid CORS issues from browser clients.
        /// Does not require authentication — public games are visible to anyone.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [Route("publicgames")]
        public async Task<IActionResult> GetPublicGames([FromQuery] int page = 1, [FromQuery] int count = 10, CancellationToken cancellationToken = default)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetPublicGames(context, page, count, cancellationToken));
        }

        /// <summary>
        /// Gets list of games where user is present; the server's local admin gets every game.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("GetGames")]
        public async Task<IActionResult> GetGames()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetGames(context));
        }

        [HttpDelete]
        [Authorize]
        [Route("DeleteGame")]
        public async Task<IActionResult> DeleteGame(Guid gameId)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.DeleteGame(context, gameId));
        }

        /// <summary>
        /// Renames a game and sets its description, from the game list. Needs Edit on the game,
        /// or being the server's local admin.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("UpdateGame")]
        public async Task<IActionResult> UpdateGame([FromBody] UpdateGameRequest request)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.UpdateGame(context, request));
        }

        [HttpGet]
        [Authorize]
        [Route("GetFeaturedAddons")]
        public async Task<IActionResult> GetFeaturedAddons([FromServices] IBuiltInAddons builtInAddons, [FromServices] AddonRegistryOptions addonRegistry)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetFeaturedAddons(context, builtInAddons, addonRegistry));
        }

        /// <summary>
        /// Adds new game
        /// </summary>
        /// <returns>Bool value informing if game is created</returns>
        [HttpPost]
        [Authorize]
        [Route("AddGame")]
        public async Task<IActionResult> AddGame([FromBody] AddGameCommand addGameCommand)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.AddGame(context, addGameCommand));
        }

        /// <summary>
        /// Retries Central Server session creation for an existing game.
        /// Call this if AddGame reported sessionCreated=false.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("assignSession")]
        public async Task<IActionResult> AssignSession([FromQuery] Guid gameId)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.AssignSession(context, gameId));
        }

        /// <summary>
        /// Adds player to game (TODO handle websocket player adding)
        /// </summary>
        /// <param name="cmd">Player information</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost]
        [Route("join")]
        public async Task<IActionResult> JoinGame([FromBody] AddPlayerCommand cmd)
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.JoinGame(context, cmd));
        }

        [Authorize]
        [HttpGet]
        [Route("versionInfo")]
        public async Task<IActionResult> GetVersionInfo()
        {
            var context = HttpContext.ToApiContext();
            return HttpContext.Respond(context, await endpoints.GetVersionInfo(context));
        }
    }
}

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Controllers
{
    /// <summary>
    /// The logic is in <see cref="BattleMapEndpoints"/>, shared with hosts without ASP.NET Core
    /// (the browser-wasm host's ApiRouter); this only binds the request and the caller.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BattleMapController : Controller
    {
        private readonly BattleMapEndpoints endpoints;

        public BattleMapController(IMediator mediator)
        {
            endpoints = new BattleMapEndpoints(mediator);
        }

        private User? CurrentUser => HttpContext.Items["User"] as User;

        /// <summary>
        /// Returns player model that belongs to logged in user in specific game
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <returns>Player DTO or Unauthorized</returns>
        [Authorize]
        [ActionName("GetPlayer")]
        [Route("GetPlayer")]
        [HttpGet]
        public async Task<IActionResult> GetPlayer(Guid gameId)
            => (await endpoints.GetPlayer(CurrentUser, gameId)).ToActionResult();

        /// <summary>
        /// Returns all game assets. If you are Gamemaster you will get full game, if player, you will get only elements that are visible to you
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <returns>Game DTO or Unauthorized</returns>
        [Authorize]
        [Route("GetFullGame")]
        [HttpGet]
        public async Task<IActionResult> GetFullGame(Guid gameId)
            => (await endpoints.GetFullGame(CurrentUser, gameId)).ToActionResult();

        /// <summary>
        /// Returns game chat. Messages are sorten in descending order by datetime
        /// </summary>
        /// <param name="gameId">Id of game</param>
        /// <param name="size">Number of messages that are returned. default 30</param>
        /// <param name="page">Number of messages skipped</param>
        /// <returns>List of MessageDTO</returns>
        [Authorize]
        [Route("GetChat")]
        [HttpGet]
        public async Task<IActionResult> GetChat(Guid gameId, int size = 30, int page = 0, Guid from = default, string filter = "")
            => (await endpoints.GetChat(CurrentUser, gameId, size, page, from, filter)).ToActionResult();

        [Authorize]
        [Route("GetLayout")]
        [HttpGet]
        public async Task<IActionResult> GetLayout(Guid gameId, Guid id)
            => (await endpoints.GetLayout(CurrentUser, gameId, id)).ToActionResult();

        [Authorize]
        [Route("GetBattlemap")]
        [HttpGet]
        public async Task<IActionResult> GetBattlemap(Guid gameId, Guid id)
            => (await endpoints.GetBattlemap(CurrentUser, gameId, id)).ToActionResult();

        [Authorize]
        [Route("GetBattlemaps")]
        [HttpGet]
        public async Task<IActionResult> GetBattlemaps(Guid gameId)
            => (await endpoints.GetBattlemaps(CurrentUser, gameId)).ToActionResult();

        [Authorize]
        [Route("GetLayouts")]
        [HttpGet]
        public async Task<IActionResult> GetLayouts(Guid gameId)
            => (await endpoints.GetLayouts(CurrentUser, gameId)).ToActionResult();

        [Authorize]
        [Route("GetCards")]
        [HttpGet]
        public async Task<IActionResult> GetCards(Guid gameId)
            => (await endpoints.GetCards(CurrentUser, gameId)).ToActionResult();

        [Authorize]
        [Route("GetTree")]
        [HttpGet]
        public async Task<IActionResult> GetTree(Guid gameId, string entityType)
            => (await endpoints.GetTree(CurrentUser, gameId, entityType)).ToActionResult();
    }
}

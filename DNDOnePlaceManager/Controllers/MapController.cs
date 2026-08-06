using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : Controller
    {
        private readonly IMediator mediator;

        public MapController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// Gets a map
        /// </summary>
        /// <returns>map</returns>
        [HttpGet]
        [Authorize]
        [Route("Get")]
        public async Task<IActionResult> GetMap(Guid mapId, Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);

            GetMapCommand cmd = new GetMapCommand()
            {
                Id = mapId,
                Player = player.Player
            };

            var res = await mediator.Send(cmd);
            return Ok(res);
        }

        /// <summary>
        /// Get Flat maps
        /// </summary>
        /// <returns>map</returns>
        [HttpGet]
        [Authorize]
        [Route("GetAllFlat")]
        public async Task<IActionResult> GetAllFlat(Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You are not a player in this game" });
            }

            GetFlatMapsCommand cmd = new GetFlatMapsCommand()
            {
                GameID = gameId,
                Player = player.Player
            };

            var res = await mediator.Send(cmd);
            return Ok(res);
        }
    }
}

using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    /// <summary>
    /// Reading a map's turn order. Changes go over the turnorder_* WebSocket commands;
    /// after each, clients fetch the order here, each seeing only what they may.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TurnOrderController : Controller
    {
        private readonly IMediator mediator;

        public TurnOrderController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>The map's turn order: hidden entries left out unless the player can edit the map.</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get(Guid gameId, Guid mapId)
        {
            var user = HttpContext.Items["User"] as User;
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });
            if (player?.Player == null)
                return Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                return Ok(await mediator.Send(new GetTurnOrderCommand { GameId = gameId, MapId = mapId, Player = player.Player }));
            }
            catch (PermissionException)
            {
                return Forbid();
            }
            catch (ResourceNotFoundException)
            {
                return NotFound();
            }
        }
    }
}

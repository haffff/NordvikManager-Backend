using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.Commands.Security.CheckPermissions;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SecurityController : ControllerBase
    {
        private IMediator mediator;
        public SecurityController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// Gets entity permissions
        /// </summary>
        /// <returns>id of map</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet]
        [Authorize]
        [Route("permissions")]
        public async Task<IActionResult> GetPermissions(Guid gameId, Guid entityId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand() { User = user, GameID = gameId };

            var player = await mediator.Send(playerCmd);

            GetPermissionsCommand cmd = new GetPermissionsCommand()
            {
                EntityId = entityId,
                Player = player.Player
            };

            var res = await mediator.Send(cmd);
            return Ok(res);
        }
    }
}

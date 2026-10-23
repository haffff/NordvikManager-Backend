using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DndOnePlaceManager.Infrastructure.Services.Sandbox;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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
        /// Gets the hosts addon cards and Html chat messages may load from
        /// (Security:SandboxAllowedOrigins). Player clients enforce it themselves.
        /// </summary>
        [Microsoft.AspNetCore.Mvc.HttpGet]
        [Authorize]
        [Route("sandboxorigins")]
        public IActionResult GetSandboxOrigins([FromServices] SandboxOrigins origins)
        {
            return Ok(new SandboxOriginsResponse { Origins = origins.Origins });
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

    public class SandboxOriginsResponse
    {
        public IReadOnlyList<string> Origins { get; set; } = new List<string>();
    }
}

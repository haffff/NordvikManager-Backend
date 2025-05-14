using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Properties.UpdateProperties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropertiesController : ControllerBase
    {
        private IMediator mediator;

        public PropertiesController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [Authorize]
        [Route("GetProperties")]
        [HttpGet]
        public async Task<IActionResult> GetProperties(Guid gameId, Guid parentId)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetPropertiesCommand cmd = new GetPropertiesCommand();
            cmd.Player = player.Player;
            cmd.ParentID = parentId;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("QueryProperties")]
        [HttpGet]
        public async Task<IActionResult> QueryProperties([FromQuery]Guid gameId, [Required]string parentIds, string? names, string? ids, string? prefix)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add Battlemap. You are not a player" });
            }

            GetPropertiesByQueryCommand cmd = new GetPropertiesByQueryCommand();
            if (!String.IsNullOrWhiteSpace(parentIds))
            {
                cmd.ParentIDs = parentIds.Split(',').Select(Guid.Parse).ToArray();
            }

            if (!String.IsNullOrWhiteSpace(ids))
            {
                cmd.Ids = ids.Split(',').Select(Guid.Parse).ToArray();
            }

            if(!String.IsNullOrWhiteSpace(names))
            {
                cmd.PropertyNames = names.Split(',');
            }

            cmd.Player = player.Player;
            cmd.Prefix = prefix;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Obsolete]
        [Authorize]
        [Route("GetSelectedIds")]
        [HttpPost]
        public async Task<IActionResult> GetSelectedIds(Guid gameId, Guid parentId, [FromBody] Guid[] ids)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot get Properties. You are not a player" });
            }

            GetPropertiesCommand cmd = new GetPropertiesCommand();
            cmd.Player = player.Player;
            cmd.ParentID = parentId;
            cmd.Ids = ids;

            var result = await mediator.Send(cmd);

            return Ok(result);
        }

        [Authorize]
        [Route("AddBulk")]
        [HttpPost]
        public async Task<IActionResult> AddBulkProperties(Guid gameId, PropertyDTO[] properties)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot add bult properties. You are not a player" });
            }

            AddPropertiesCommand addPropertiesCmd = new AddPropertiesCommand()
            {
                GameID = gameId,
                Player = player.Player,
                Properties = properties,
            };

            var result = await mediator.Send(addPropertiesCmd);
            if (result != CommandResponse.Ok)
            {
                return BadRequest(new { result = result } );
            }
            return Ok(new { result = result });
        }


        [Authorize]
        [Route("UpdateBulk")]
        [HttpPost]
        public async Task<IActionResult> UpdateBulkProperties([FromQuery]Guid gameId, [FromBody]PropertyDTO[] properties)
        {
            var currentUser = HttpContext.Items["User"] as User;

            GetPlayerCommand playerCmd = new GetPlayerCommand();
            playerCmd.GameID = gameId;
            playerCmd.User = currentUser;

            var player = await mediator.Send(playerCmd);
            if (player?.Player == null)
            {
                return Unauthorized(new { error = "You cannot update bult properties. You are not a player" });
            }

            UpdatePropertiesCommand updatePropertiesCommand = new UpdatePropertiesCommand()
            {
                GameID = gameId,
                Player = player.Player,
                Properties = properties,
            };

            var result = await mediator.Send(updatePropertiesCommand);

            if (result != CommandResponse.Ok)
            {
                return BadRequest(new { result = result });
            }

            return Ok(new { result = result });
        }
    }
}

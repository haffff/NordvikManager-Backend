using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Application.Extension;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialsController : Controller
    {
        private IMediator mediator;

        public MaterialsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// Get Resource from database
        /// </summary>
        /// <param name="id">Id of resource</param>
        /// <param name="gameId">Id of game</param>
        /// <returns>File in bytes with specified mime type</returns>
        [HttpGet]
        [Authorize]
        [Route("Resource")]
        public async Task<IActionResult> GetResource(Guid id, string? key, Guid? gameId)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
            {
                return BadRequest();
            }

            GetResourceDataCommand imageCommand = new GetResourceDataCommand();
            imageCommand.GameID = gameId;
            imageCommand.Player = playerResult.Player;
            imageCommand.ID = id;
            imageCommand.Key = key;

            var result = await mediator.Send(imageCommand);

            if (result.Item1 == null)
            {
                return BadRequest();
            }

            return File(result.Item1, result.Item2.GetDescriptionValue());
        }

        /// <summary>
        /// Get Resource from database
        /// </summary>
        /// <param name="id">Id of resource</param>
        /// <param name="gameId">Id of game</param>
        /// <returns>File in bytes with specified mime type</returns>
        [HttpGet]
        [Authorize]
        [Route("ResourceMetadata")]
        public async Task<IActionResult> GetResourceMetadata(Guid id, Guid? gameId)
        {
            var user = HttpContext.Items["User"] as User;

            if (gameId != null)
            {
                GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameId, user);
                if (playerResult?.Player == null)
                {
                    return BadRequest();
                }
            }

            GetResourceCommand imageCommand = new GetResourceCommand();
            imageCommand.Player = (await GetPlayerIfExists(gameId, user)).Player;
            imageCommand.ResourceId = id;

            var result = await mediator.Send(imageCommand);

            return Ok(result);
        }


        [HttpGet]
        [Authorize]
        [Route("GetTemplates")]
        public async Task<IActionResult> GetTemplates(Guid? gameID)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return BadRequest();
            }

            var getCardsCommand = new GetAllCardsCommand
            {
                GameId = gameID ?? default,
                Player = playerResult.Player,
                CustomUis = false,
                Templates = true,
                Flat = true
            };

            var (resp, result) = await mediator.Send(getCardsCommand);

            return Ok(result);
        }


        [HttpGet]
        [Authorize]
        [Route("GetTemplatesFull")]
        public async Task<IActionResult> GetTemplatesFull(Guid? gameID)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return BadRequest();
            }

            var getCardsCommand = new GetAllCardsCommand
            {
                GameId = gameID ?? default,
                Player = playerResult.Player,
                CustomUis = false,
                Templates = true,
                Flat = false
            };

            var (resp, result) = await mediator.Send(getCardsCommand);

            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("GetCards")]
        public async Task<IActionResult> GetCards(Guid? gameID)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return BadRequest();
            }

            var getCardsCommand = new GetAllCardsCommand
            {
                GameId = gameID ?? default,
                Player = playerResult.Player,
                CustomUis = false,
                Templates = false,
                Flat = true
            };

            var (resp, result) = await mediator.Send(getCardsCommand);

            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("GetCard")]
        public async Task<IActionResult> GetCard(Guid? gameID, Guid? id)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return BadRequest();
            }

            var getCardsCommand = new GetCardCommand
            {
                GameID = gameID ?? default,
                Player = playerResult.Player,
                Id = id ?? default
            };

            var result = await mediator.Send(getCardsCommand);

            return Ok(result);
        }

        [HttpGet]
        [Authorize]
        [Route("GetResources")]
        public async Task<IActionResult> GetResources(Guid? gameID)
        {
            var user = HttpContext.Items["User"] as User;

            GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameID, user);

            GetResourcesCommand getResourcesCommand = new GetResourcesCommand();
            getResourcesCommand.GameId = gameID;
            getResourcesCommand.Player = playerResult.Player;

            var (response, result) = await mediator.Send(getResourcesCommand);

            return Ok(result);
        }


        [HttpPost]
        [Authorize]
        [Route("AddResource")]
        public async Task<IActionResult> AddResource([FromQuery]Guid gameId , [FromBody] AddResourceRequest request) 
        {
            var user = HttpContext.Items["User"] as User;

            var command = new AddResourceCommand()
            {
                Name = request.Name,
                MimeType = request.MimeType,
                Key = request.Key,
                ParentFolder = request.ParentFolder,
                GameID = gameId,
                Data = request.Data
            };

            //TODO, this should be form multipart with IFormFile
            Regex r = new Regex(@"data:(?<type>\w+/\w+);base64,");
            command.Data = r.Replace(command.Data, "", 1);
            command.Player = (await GetPlayerIfExists(command.GameID, user)).Player;

            (var result, var id) = await mediator.Send(command);

            WebSockets.WebSocketManager.SendCommandToUser(user, new WebSocketCommand { Command = "resource_notify" });

            return Ok(new { Id = id });
        }

        [HttpDelete]
        [Authorize]
        [Route("RemoveResource")]
        public async Task<IActionResult> RemoveResource([FromQuery] Guid? gameId, [FromBody] Guid? ID)
        {
            var user = HttpContext.Items["User"] as User;

            var playerResult = await GetPlayerIfExists(null, user);

            var removeCommand = new RemoveResourceCommand();

            removeCommand.ID = ID;
            removeCommand.Player = playerResult.Player;
            removeCommand.GameId = gameId;

            var result = await mediator.Send(removeCommand);

            return Ok(result);
        }

        private async Task<GetPlayerCommandResponse> GetPlayerIfExists(Guid? gameID, User user)
        {
            GetPlayerCommand getPlayer = new GetPlayerCommand();
            getPlayer.GameID = gameID;
            getPlayer.User = user;

            var playerResult = await mediator.Send(getPlayer);
            return playerResult;
        }
    }
}

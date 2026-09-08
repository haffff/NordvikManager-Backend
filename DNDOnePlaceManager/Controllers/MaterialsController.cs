using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData;
using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Application.Commands.Resources.Link;
using DndOnePlaceManager.Application.Commands.Resources.Transfer;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers.Requests;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.WebSockets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ILobbyService _lobbyService;
        private readonly IServiceScopeFactory _scopeFactory;

        public MaterialsController(IMediator mediator, ILobbyService lobbyService, IServiceScopeFactory scopeFactory)
        {
            this.mediator = mediator;
            _lobbyService = lobbyService;
            _scopeFactory = scopeFactory;
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
        public async Task<IActionResult> AddResource([FromQuery] Guid gameId, [FromBody] AddResourceRequest request)
        {
            var user = HttpContext.Items["User"] as User;

            var command = new AddResourceCommand()
            {
                Name = request.Name,
                MimeType = request.MimeType,
                Key = request.Key,
                ParentFolder = request.ParentFolder,
                GameID = gameId,
                Data = request.Data,
                StorageKind = request.StorageKind,
            };

            //TODO, this should be form multipart with IFormFile
            Regex r = new Regex(@"data:(?<type>\w+/\w+);base64,");
            command.Data = r.Replace(command.Data, "", 1);
            command.Player = (await GetPlayerIfExists(command.GameID, user)).Player;

            (var result, var id) = await mediator.Send(command);

            _lobbyService.SendCommandToUser(user, new WebSocketCommand { Command = "resource_notify" });

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

        /// <summary>
        /// Returns resource bytes as base64 JSON — used by the WebRTC tunnel where
        /// raw binary File() responses are not usable over a data channel.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("ResourceWebRTC")]
        public async Task<IActionResult> GetResourceWebRTC(Guid id, string? key, Guid? gameId)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new GetResourceDataCommand
            {
                GameID = gameId,
                Player = playerResult.Player,
                ID     = id,
                Key    = key,
            });

            if (result.Item1 == null)
                return NotFound();

            return Ok(new
            {
                data     = Convert.ToBase64String(result.Item1),
                mimeType = result.Item2.GetDescriptionValue(),
            });
        }

        /// <summary>
        /// Creates a resource with a stable key; returns 409 if key already exists.
        /// Used by addons via the WebRTC tunnel.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("CreateResource")]
        public async Task<IActionResult> CreateResource([FromQuery] Guid gameId, [FromBody] CreateResourceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Key) && string.IsNullOrWhiteSpace(request?.Name))
                return BadRequest(new { error = "key or name is required." });

            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            byte[] data;
            try
            {
                data = string.IsNullOrEmpty(request.Content)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(request.Content);
            }
            catch
            {
                return BadRequest(new { error = "content must be a valid base64 string." });
            }

            var (resp, id) = await mediator.Send(new CreateResourceCommand
            {
                GameId   = gameId,
                Player   = playerResult.Player,
                Key      = request.Key,
                Name     = request.Name ?? request.Key ?? string.Empty,
                Data     = data,
                MimeType = request.MimeType,
                StorageKind = request.StorageKind,
            });

            if (resp == CommandResponse.AlreadyExists)
                return Conflict(new { error = $"Resource with key '{request.Key}' already exists." });

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby != null)
            {
                await lobby.HandlePostCommand(playerResult.Player, new WebSockets.WebSocketCommand
                {
                    Command = WebSockets.Core.WebSocketCommandNames.ResourceAdd,
                    Result  = WebSockets.Core.WebSocketCommandNames.ResultOk,
                    Data    = Newtonsoft.Json.Linq.JToken.FromObject(new
                    {
                        id,
                        name       = request.Name ?? request.Key,
                        path       = (string?)null,
                        mimeType   = request.MimeType,
                        playerId   = playerResult.Player.Id,
                        playerName = playerResult.Player.Name,
                    }),
                });
            }

            return Ok(new { id });
        }

        /// <summary>
        /// Overwrites the binary content of an existing resource (by id or key).
        /// </summary>
        [HttpPut]
        [Authorize]
        [Route("ResourceData")]
        public async Task<IActionResult> UpdateResourceData(
            [FromQuery] Guid gameId, [FromBody] UpdateResourceDataRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Key) && request?.Id == null)
                return BadRequest(new { error = "key or id is required." });

            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var (_, id) = await mediator.Send(new UpdateResourceDataCommand
            {
                GameId  = gameId,
                Player  = playerResult.Player,
                Key     = request.Key,
                Id      = request.Id,
                Content = request.Content,
                MimeType = request.MimeType,
            });

            return Ok(new { id });
        }

        /// <summary>
        /// Deletes a resource (and its tree entries) by id or key.
        /// </summary>
        [HttpDelete]
        [Authorize]
        [Route("ResourceData")]
        public async Task<IActionResult> DeleteResourceData(
            [FromQuery] Guid gameId, [FromQuery] string? key, [FromQuery] Guid? id)
        {
            if (string.IsNullOrWhiteSpace(key) && id == null)
                return BadRequest(new { error = "key or id query parameter is required." });

            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            await mediator.Send(new DeleteResourceDataCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Key    = key,
                Id     = id,
            });

            return Ok();
        }

        /// <summary>
        /// Registers a reference to a file already on the local disk of whoever runs this
        /// backend — zero bytes copied. GM-only (enforced in the handler).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("LinkResource")]
        public async Task<IActionResult> LinkResource([FromQuery] Guid gameId, [FromBody] LinkResourceRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var (_, id) = await mediator.Send(new LinkResourceCommand
            {
                GameId       = gameId,
                Player       = playerResult.Player,
                Name         = request.Name,
                LocalPath    = request.LocalPath,
                MimeType     = request.MimeType,
                ParentFolder = request.ParentFolder,
            });

            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby != null)
            {
                await lobby.HandlePostCommand(playerResult.Player, new WebSockets.WebSocketCommand
                {
                    Command = WebSockets.Core.WebSocketCommandNames.ResourceAdd,
                    Result  = WebSockets.Core.WebSocketCommandNames.ResultOk,
                    Data    = Newtonsoft.Json.Linq.JToken.FromObject(new
                    {
                        id,
                        name       = request.Name,
                        storage    = ResourceStorageKind.Linked.ToString(),
                        mimeType   = request.MimeType,
                        playerId   = playerResult.Player.Id,
                        playerName = playerResult.Player.Name,
                    }),
                });
            }

            return Ok(new { id });
        }

        /// <summary>
        /// Recursively links every file found under a local directory (GM-only). Large folders
        /// can take a while to walk, so the actual linking runs in a background task and this
        /// returns as soon as it's scheduled — progress/completion/failure are broadcast over
        /// the lobby's WS/WebRTC channel (operation_progress/complete/failed, keyed by the
        /// returned operationId) instead of being returned in this response.
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("LinkDirectory")]
        public async Task<IActionResult> LinkDirectory([FromQuery] Guid gameId, [FromBody] LinkDirectoryRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var player = playerResult.Player;
            var operationId = Guid.NewGuid();

            // Fire-and-forget: the request's own DI scope (and its IDbContext) is disposed the
            // moment this action returns, so the background walk resolves its own IMediator/
            // ILobbyService from a fresh scope rather than reusing the controller's fields.
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var scopedLobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();
                var lobby = scopedLobbyService.GetLobby(gameId);

                try
                {
                    var (_, linkedCount) = await scopedMediator.Send(new LinkDirectoryCommand
                    {
                        GameId             = gameId,
                        Player             = player,
                        LocalDirectoryPath = request.LocalDirectoryPath,
                        ParentFolder       = request.ParentFolder,
                        OnProgress         = count => OperationProgressPublisher.Update(lobby, player, operationId, count).GetAwaiter().GetResult(),
                    });

                    await OperationProgressPublisher.Complete(lobby, player, operationId, description: $"Linked {linkedCount} file(s)");
                }
                catch (Exception ex)
                {
                    await OperationProgressPublisher.Fail(lobby, player, operationId, "Failed to link folder", ex.Message);
                }
            });

            return Ok(new { started = true, operationId });
        }

        /// <summary>
        /// Lists a local directory's contents (GM-only) — omit path to list drive roots.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("BrowseLocalDirectory")]
        public async Task<IActionResult> BrowseLocalDirectory([FromQuery] Guid gameId, [FromQuery] string? path)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var entries = await mediator.Send(new BrowseLocalDirectoryCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Path   = path,
            });

            return Ok(entries);
        }

        /// <summary>
        /// Moves an existing resource's bytes between storage modes (GM-only).
        /// </summary>
        [HttpPost]
        [Authorize]
        [Route("TransferResourceStorage")]
        public async Task<IActionResult> TransferResourceStorage([FromQuery] Guid gameId, [FromBody] TransferResourceStorageRequest request)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var result = await mediator.Send(new TransferResourceStorageCommand
            {
                GameId        = gameId,
                Player        = playerResult.Player,
                ResourceId    = request.ResourceId,
                TargetStorage = request.TargetStorage,
            });

            var lobby = _lobbyService.GetLobby(gameId);
            if (result == CommandResponse.Ok && lobby != null)
            {
                await lobby.HandlePostCommand(playerResult.Player, new WebSockets.WebSocketCommand
                {
                    Command = WebSockets.Core.WebSocketCommandNames.ResourceUpdate,
                    Result  = WebSockets.Core.WebSocketCommandNames.ResultOk,
                    Data    = Newtonsoft.Json.Linq.JToken.FromObject(new
                    {
                        id      = request.ResourceId,
                        storage = request.TargetStorage.ToString(),
                    }),
                });
            }

            return Ok(new { response = result });
        }

        /// <summary>
        /// Returns all custom UI cards for the game.
        /// Route aliases preserve the legacy typo used by WebRTC clients.
        /// </summary>
        [HttpGet]
        [Authorize]
        [Route("GetCustomViews")]
        [Route("GetCustomWiews")]
        public async Task<IActionResult> GetCustomViews([FromQuery] Guid gameId)
        {
            var user = HttpContext.Items["User"] as User;
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return BadRequest();

            var (_, result) = await mediator.Send(new GetAllCardsCommand
            {
                GameId    = gameId,
                Player    = playerResult.Player,
                Templates = false,
                CustomUis = true,
            });

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

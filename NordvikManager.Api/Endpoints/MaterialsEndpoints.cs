using NordvikManager.Application.Commands.Card.GetAllCards;
using NordvikManager.Application.Commands.Card.GetCard;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Commands.Resources.GetResourceContent;
using NordvikManager.Application.Commands.Resources.GetResourceRange;
using NordvikManager.Application.Commands.Resources.Upload;
using NordvikManager.Application.Commands.Resources.CreateResource;
using NordvikManager.Application.Commands.Resources.DeleteResourceData;
using NordvikManager.Application.Commands.Resources.GetResource;
using NordvikManager.Application.Commands.Resources.Link;
using NordvikManager.Application.Commands.Resources.Transfer;
using NordvikManager.Application.Commands.Resources.UpdateResourceData;
using NordvikManager.Application.Extension;
using NordvikManager.Domain.Enums;
using NordvikManager.Controllers.Requests;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;
using NordvikManager.WebSockets;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NordvikManager.Api;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Materials (MaterialsController delegates here).</summary>
    public sealed class MaterialsEndpoints
    {
        private IMediator mediator;
        private readonly ILobbyService _lobbyService;
        private readonly IServiceScopeFactory _scopeFactory;

        public MaterialsEndpoints(IMediator mediator, ILobbyService lobbyService, IServiceScopeFactory scopeFactory)
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
        [ApiRoute("GET", "api/Materials/Resource")]
        public async Task<ApiResult> GetResource(User? user, Guid id, string? key, Guid? gameId, bool thumbnail = false)
        {

            GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
            {
                return ApiResult.BadRequest();
            }

            (byte[], MimeType) result;
            if (thumbnail)
            {
                result = await mediator.Send(new GetResourceThumbnailCommand
                {
                    GameID = gameId,
                    Player = playerResult.Player,
                    ID = id,
                    Key = key,
                });
            }
            else
            {
                GetResourceDataCommand imageCommand = new GetResourceDataCommand();
                imageCommand.GameID = gameId;
                imageCommand.Player = playerResult.Player;
                imageCommand.ID = id;
                imageCommand.Key = key;

                result = await mediator.Send(imageCommand);
            }

            if (result.Item1 == null)
            {
                return ApiResult.BadRequest();
            }

            return ApiResult.File(result.Item1, result.Item2.GetDescriptionValue());
        }

        /// <summary>
        /// Get Resource from database
        /// </summary>
        /// <param name="id">Id of resource</param>
        /// <param name="gameId">Id of game</param>
        /// <returns>File in bytes with specified mime type</returns>
        [ApiRoute("GET", "api/Materials/ResourceMetadata")]
        public async Task<ApiResult> GetResourceMetadata(User? user, Guid id, Guid? gameId)
        {

            if (gameId != null)
            {
                GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameId, user);
                if (playerResult?.Player == null)
                {
                    return ApiResult.BadRequest();
                }
            }

            GetResourceCommand imageCommand = new GetResourceCommand();
            imageCommand.Player = (await GetPlayerIfExists(gameId, user)).Player;
            imageCommand.ResourceId = id;
            imageCommand.IsLocalAdmin = user?.IsLocalAdmin == true;

            var result = await mediator.Send(imageCommand);

            return ApiResult.Ok(result);
        }


        [ApiRoute("GET", "api/Materials/GetTemplates")]
        public async Task<ApiResult> GetTemplates(User? user, Guid? gameID)
        {

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return ApiResult.BadRequest();
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

            return ApiResult.Ok(result);
        }


        [ApiRoute("GET", "api/Materials/GetTemplatesFull")]
        public async Task<ApiResult> GetTemplatesFull(User? user, Guid? gameID)
        {

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return ApiResult.BadRequest();
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

            return ApiResult.Ok(result);
        }

        [ApiRoute("GET", "api/Materials/GetCards")]
        public async Task<ApiResult> GetCards(User? user, Guid? gameID)
        {

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return ApiResult.BadRequest();
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

            return ApiResult.Ok(result);
        }

        [ApiRoute("GET", "api/Materials/GetCard")]
        public async Task<ApiResult> GetCard(User? user, Guid? gameID, Guid? id)
        {

            var playerResult = await GetPlayerIfExists(gameID, user);
            if (playerResult?.Player == null)
            {
                return ApiResult.BadRequest();
            }

            var getCardsCommand = new GetCardCommand
            {
                GameID = gameID ?? default,
                Player = playerResult.Player,
                Id = id ?? default
            };

            var result = await mediator.Send(getCardsCommand);

            return ApiResult.Ok(result);
        }

        [ApiRoute("GET", "api/Materials/GetResources")]
        public async Task<ApiResult> GetResources(User? user, Guid? gameID)
        {

            GetPlayerCommandResponse playerResult = await GetPlayerIfExists(gameID, user);

            GetResourcesCommand getResourcesCommand = new GetResourcesCommand();
            getResourcesCommand.GameId = gameID;
            getResourcesCommand.Player = playerResult.Player;
            getResourcesCommand.IsLocalAdmin = user?.IsLocalAdmin == true;

            var (response, result) = await mediator.Send(getResourcesCommand);

            return ApiResult.Ok(result);
        }


        [ApiRoute("POST", "api/Materials/AddResource")]
        public async Task<ApiResult> AddResource(User? user, Guid gameId, AddResourceRequest request)
        {

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

            return ApiResult.Ok(new { Id = id });
        }

        [ApiRoute("DELETE", "api/Materials/RemoveResource")]
        public async Task<ApiResult> RemoveResource(User? user, Guid? gameId, [ApiBody] Guid? ID)
        {

            var playerResult = await GetPlayerIfExists(null, user);

            var removeCommand = new RemoveResourceCommand();

            removeCommand.ID = ID;
            removeCommand.Player = playerResult.Player;
            removeCommand.GameId = gameId;

            var result = await mediator.Send(removeCommand);

            return ApiResult.Ok(result);
        }

        /// <summary>
        /// Returns resource bytes as base64 JSON — used by the WebRTC tunnel where
        /// raw binary File() responses are not usable over a data channel.
        /// </summary>
        [ApiRoute("GET", "api/Materials/ResourceWebRTC")]
        public async Task<ApiResult> GetResourceWebRTC(User? user, Guid id, string? key, Guid? gameId, bool thumbnail = false, string? ifVersion = null)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            // The client sends the version it has cached; headers don't cross the WebRTC
            // tunnel, so the version and "not modified" travel in the body.
            var content = await mediator.Send(new GetResourceContentCommand
            {
                GameID     = gameId,
                Player     = playerResult.Player,
                ID         = id,
                Key        = key,
                Thumbnail  = thumbnail,
                IfVersion  = ifVersion,
            });

            if (content == null)
                return ApiResult.NotFound();

            if (content.NotModified)
                return ApiResult.Ok(new { notModified = true, version = content.Version });

            return ApiResult.Ok(new
            {
                data     = Convert.ToBase64String(content.Data!),
                mimeType = content.MimeType.GetDescriptionValue(),
                version  = content.Version,
            });
        }

        /// <summary>
        /// Part of a resource's bytes as base64 JSON (at most 2 MB), for readers that need only
        /// some of a big file, such as pdf.js showing a few pages of a book.
        /// </summary>
        [ApiRoute("GET", "api/Materials/ResourceRange")]
        public async Task<ApiResult> GetResourceRange(User? user, Guid? id, string? key, Guid? gameId, long start = 0, int length = 0, string? ifVersion = null)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var range = await mediator.Send(new GetResourceRangeCommand
            {
                GameID    = gameId,
                Player    = playerResult.Player,
                ID        = id,
                Key       = key,
                Start     = start,
                Length    = length,
                IfVersion = ifVersion,
            });

            if (range == null)
                return ApiResult.NotFound();

            if (range.NotModified)
                return ApiResult.Ok(new { notModified = true, start = range.Start, total = range.Total, version = range.Version });

            return ApiResult.Ok(new
            {
                data     = Convert.ToBase64String(range.Data!),
                start    = range.Start,
                total    = range.Total,
                mimeType = range.MimeType.GetDescriptionValue(),
                version  = range.Version,
            });
        }

        /// <summary>
        /// Creates a resource with a stable key; returns 409 if key already exists.
        /// Used by addons via the WebRTC tunnel.
        /// </summary>
        /// <summary>
        /// POST materials/BuildToken — a token's canvas object (as a JSON string) and its size,
        /// built on the server the way the SpawnToken step builds one; the client then adds it
        /// to the map with element_add.
        /// </summary>
        [ApiRoute("POST", "api/Materials/BuildToken")]
        public async Task<ApiResult> BuildToken(User? user, Guid gameId, NordvikManager.Models.BuildTokenRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            try
            {
                var token = await mediator.Send(new NordvikManager.Application.Commands.Actions.BuildToken.BuildTokenCommand
                {
                    GameId = gameId, Player = playerResult.Player, MapId = request.MapId, CardId = request.CardId,
                    Token = request.Token, Image = request.Image, X = request.X, Y = request.Y,
                });
                return ApiResult.Ok(new { @object = token.Object.ToString(Newtonsoft.Json.Formatting.None), size = token.Size });
            }
            catch (ArgumentException e)
            {
                return ApiResult.BadRequest(new { error = e.Message });
            }
        }

        [ApiRoute("POST", "api/Materials/CreateResource")]
        public async Task<ApiResult> CreateResource(User? user, Guid gameId, CreateResourceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Key) && string.IsNullOrWhiteSpace(request?.Name))
                return ApiResult.BadRequest(new { error = "key or name is required." });

            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            byte[] data;
            try
            {
                data = string.IsNullOrEmpty(request.Content)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(request.Content);
            }
            catch
            {
                return ApiResult.BadRequest(new { error = "content must be a valid base64 string." });
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
                return ApiResult.Conflict(new { error = $"Resource with key '{request.Key}' already exists." });

            await AnnounceNewResourceAsync(gameId, playerResult.Player, id, request.Name ?? request.Key, request.MimeType);
            return ApiResult.Ok(new { id });
        }

        // resource_add for a resource made by key (CreateResource, a finished upload with a key);
        // the lobby sends it to its maker and the GMs.
        private async Task AnnounceNewResourceAsync(Guid gameId, NordvikManager.Application.DataTransferObjects.Game.PlayerDTO player, Guid? id, string? name, string? mimeType)
        {
            var lobby = _lobbyService.GetLobby(gameId);
            if (lobby == null)
                return;
            await lobby.HandlePostCommand(player, new WebSockets.WebSocketCommand
            {
                Command = WebSockets.Core.WebSocketCommandNames.ResourceAdd,
                Result  = WebSockets.Core.WebSocketCommandNames.ResultOk,
                Data    = Newtonsoft.Json.Linq.JToken.FromObject(new
                {
                    id,
                    name,
                    path       = (string?)null,
                    mimeType,
                    playerId   = player.Id,
                    playerName = player.Name,
                }),
            });
        }

        // ── Uploads in parts: a big file without one huge request, resumable after a drop ──

        [ApiRoute("POST", "api/Materials/UploadStart")]
        public async Task<ApiResult> UploadStart(User? user, Guid gameId, UploadStartRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var (response, uploadId) = await mediator.Send(new StartUploadCommand
            {
                GameId = gameId, Player = playerResult.Player, Name = request.Name, MimeType = request.MimeType, Key = request.Key,
                ParentFolder = request.ParentFolder, StorageKind = request.StorageKind, Size = request.Size,
                ForImport = request.ForImport,
            });
            return response switch
            {
                CommandResponse.Ok => ApiResult.Ok(new { uploadId }),
                CommandResponse.AlreadyExists => ApiResult.Conflict(new { error = $"Resource with key '{request.Key}' already exists." }),
                _ => ApiResult.BadRequest(new { error = request.ForImport ? "A name and a size up to 512 MB are needed." : "A name and a size up to 1 GB are needed." }),
            };
        }

        [ApiRoute("POST", "api/Materials/UploadPart")]
        public async Task<ApiResult> UploadPart(User? user, Guid gameId, UploadPartRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            byte[] data;
            try { data = Convert.FromBase64String(request.Data ?? ""); }
            catch { return ApiResult.BadRequest(new { error = "data must be base64." }); }
            if (data.Length > GetResourceRangeCommandHandler.MaxLength)
                return ApiResult.BadRequest(new { error = "A part is at most 2 MB." });

            var (response, received) = await mediator.Send(new UploadPartCommand { UploadId = request.UploadId, Player = playerResult.Player, Offset = request.Offset, Data = data });
            return response switch
            {
                CommandResponse.Ok => ApiResult.Ok(new { received }),
                CommandResponse.WrongArguments => ApiResult.Conflict(new { error = "The upload continues elsewhere.", received }),
                _ => ApiResult.NotFound(),
            };
        }

        [ApiRoute("GET", "api/Materials/UploadStatus")]
        public async Task<ApiResult> UploadStatus(User? user, Guid gameId, Guid uploadId)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var received = await mediator.Send(new GetUploadStatusCommand { UploadId = uploadId, Player = playerResult.Player });
            return received == null ? ApiResult.NotFound() : ApiResult.Ok(new { received });
        }

        [ApiRoute("POST", "api/Materials/UploadFinish")]
        public async Task<ApiResult> UploadFinish(User? user, Guid gameId, UploadFinishRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var result = await mediator.Send(new FinishUploadCommand { UploadId = request.UploadId, Player = playerResult.Player });
            switch (result.Response)
            {
                case CommandResponse.Ok:
                    if (result.ByKey)
                        await AnnounceNewResourceAsync(result.GameId, playerResult.Player, result.ResourceId, result.Name, result.MimeType);
                    else
                        _lobbyService.SendCommandToUser(user, new WebSocketCommand { Command = "resource_notify" });
                    return ApiResult.Ok(new { id = result.ResourceId });
                case CommandResponse.NoResource:
                    return ApiResult.NotFound();
                case CommandResponse.AlreadyExists:
                    return ApiResult.Conflict(new { error = "A resource with that key already exists." });
                default:
                    return ApiResult.BadRequest(new { error = "The upload is not complete." });
            }
        }

        /// <summary>
        /// Overwrites the binary content of an existing resource (by id or key).
        /// </summary>
        [ApiRoute("PUT", "api/Materials/ResourceData")]
        public async Task<ApiResult> UpdateResourceData(User? user, Guid gameId, UpdateResourceDataRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Key) && request?.Id == null)
                return ApiResult.BadRequest(new { error = "key or id is required." });

            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var (_, id) = await mediator.Send(new UpdateResourceDataCommand
            {
                GameId  = gameId,
                Player  = playerResult.Player,
                Key     = request.Key,
                Id      = request.Id,
                Content = request.Content,
                MimeType = request.MimeType,
            });

            return ApiResult.Ok(new { id });
        }

        /// <summary>
        /// Deletes a resource (and its tree entries) by id or key.
        /// </summary>
        [ApiRoute("DELETE", "api/Materials/ResourceData")]
        public async Task<ApiResult> DeleteResourceData(User? user, Guid gameId, string? key, Guid? id)
        {
            if (string.IsNullOrWhiteSpace(key) && id == null)
                return ApiResult.BadRequest(new { error = "key or id query parameter is required." });

            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            await mediator.Send(new DeleteResourceDataCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Key    = key,
                Id     = id,
            });

            return ApiResult.Ok();
        }

        /// <summary>
        /// Registers a reference to a file already on the local disk of whoever runs this
        /// backend — zero bytes copied. Local admin only (enforced in the handler).
        /// </summary>
        [ApiRoute("POST", "api/Materials/LinkResource", NativeOnly = true)] // files on the server's own disk
        public async Task<ApiResult> LinkResource(User? user, Guid gameId, LinkResourceRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var (_, id) = await mediator.Send(new LinkResourceCommand
            {
                GameId       = gameId,
                Player       = playerResult.Player,
                Name         = request.Name,
                LocalPath    = request.LocalPath,
                MimeType     = request.MimeType,
                ParentFolder = request.ParentFolder,
                IsLocalAdmin = user?.IsLocalAdmin == true,
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

            return ApiResult.Ok(new { id });
        }

        /// <summary>
        /// Recursively links every file found under a local directory (local admin only). Large folders
        /// can take a while to walk, so the actual linking runs in a background task and this
        /// returns as soon as it's scheduled — progress/completion/failure are broadcast over
        /// the lobby's WS/WebRTC channel (operation_progress/complete/failed, keyed by the
        /// returned operationId) instead of being returned in this response.
        /// </summary>
        [ApiRoute("POST", "api/Materials/LinkDirectory", NativeOnly = true)] // files on the server's own disk
        public async Task<ApiResult> LinkDirectory(User? user, Guid gameId, LinkDirectoryRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            // Checked here as well as in the handler: the walk runs in the background, so a
            // refusal there would only surface as a failed-operation toast.
            if (user?.IsLocalAdmin != true)
                return ApiResult.Forbid();

            var player = playerResult.Player;
            var operationId = Guid.NewGuid();
            OperationProgressPublisher.Begin(operationId, player);

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
                        IsLocalAdmin       = true,
                        OnProgress         = count => OperationProgressPublisher.Update(lobby, player, operationId, count).GetAwaiter().GetResult(),
                    });

                    await OperationProgressPublisher.Complete(lobby, player, operationId, description: $"Linked {linkedCount} file(s)");
                }
                catch (Exception ex)
                {
                    await OperationProgressPublisher.Fail(lobby, player, operationId, "Failed to link folder", ex.Message);
                }
            });

            return ApiResult.Ok(new { started = true, operationId });
        }

        /// <summary>
        /// Lists a local directory's contents (local admin only) — omit path to list drive roots.
        /// </summary>
        [ApiRoute("GET", "api/Materials/BrowseLocalDirectory", NativeOnly = true)] // files on the server's own disk
        public async Task<ApiResult> BrowseLocalDirectory(User? user, Guid gameId, string? path)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var entries = await mediator.Send(new BrowseLocalDirectoryCommand
            {
                GameId = gameId,
                Player = playerResult.Player,
                Path   = path,
                IsLocalAdmin = user?.IsLocalAdmin == true,
            });

            return ApiResult.Ok(entries);
        }

        /// <summary>
        /// Moves an existing resource's bytes between storage modes (needs Edit on the game).
        /// </summary>
        [ApiRoute("POST", "api/Materials/TransferResourceStorage", NativeOnly = true)] // files on the server's own disk
        public async Task<ApiResult> TransferResourceStorage(User? user, Guid gameId, TransferResourceStorageRequest request)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

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

            return ApiResult.Ok(new { response = result });
        }

        /// <summary>
        /// Returns all custom UI cards for the game.
        /// Route aliases preserve the legacy typo used by WebRTC clients.
        /// </summary>
        [ApiRoute("GET", "api/Materials/GetCustomViews")]
        [ApiRoute("GET", "api/Materials/GetCustomWiews")]
        public async Task<ApiResult> GetCustomViews(User? user, Guid gameId)
        {
            var playerResult = await GetPlayerIfExists(gameId, user);
            if (playerResult?.Player == null)
                return ApiResult.BadRequest();

            var (_, result) = await mediator.Send(new GetAllCardsCommand
            {
                GameId    = gameId,
                Player    = playerResult.Player,
                Templates = false,
                CustomUis = true,
            });

            return ApiResult.Ok(result);
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

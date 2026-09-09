using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.Commands.Resources.UpdateResourceData;
using DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>
    /// Ported from DNDOnePlaceManager/Controllers/MaterialsController.cs. GetResource itself
    /// (binary File() response) isn't usable over a data channel; its data-channel-safe
    /// sibling GetResourceWebRTC (base64 JSON) is used instead, same as the real controller's
    /// own doc comment already prescribes. Filesystem-linking actions (LinkResource,
    /// LinkDirectory, BrowseLocalDirectory, TransferResourceStorage) are deliberately not
    /// ported — meaningless for a browser-wasm host, no local filesystem to link against.
    /// </summary>
    public class MaterialsRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;
        private readonly ILobbyRegistry _lobbyRegistry;

        public MaterialsRestHandler(IMediator mediator, ILobbyRegistry lobbyRegistry)
        {
            _mediator = mediator;
            _lobbyRegistry = lobbyRegistry;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();
            var method = (request.Method ?? string.Empty).ToUpperInvariant();

            switch ($"{method} {path}")
            {
                case "GET api/materials/resourcewebrtc":
                {
                    Guid.TryParse(request.Query.Get("id"), out var id);
                    var key = request.Query.Get("key");

                    var result = await _mediator.Send(new GetResourceDataCommand
                    {
                        GameID = gameId,
                        Player = player,
                        ID = id,
                        Key = key
                    });

                    if (result.Item1 == null)
                        return new WebRTCApiResult(404, null);

                    return new WebRTCApiResult(200, new
                    {
                        data = Convert.ToBase64String(result.Item1),
                        mimeType = result.Item2.GetDescriptionValue()
                    });
                }

                case "GET api/materials/getresources":
                {
                    var (_, resources) = await _mediator.Send(new GetResourcesCommand { GameId = gameId, Player = player });
                    return new WebRTCApiResult(200, resources);
                }

                case "GET api/materials/gettemplates":
                {
                    var (_, cards) = await _mediator.Send(new GetAllCardsCommand
                    {
                        GameId = gameId,
                        Player = player,
                        CustomUis = false,
                        Templates = true,
                        Flat = true
                    });
                    return new WebRTCApiResult(200, cards);
                }

                case "GET api/materials/gettemplatesfull":
                {
                    var (_, cards) = await _mediator.Send(new GetAllCardsCommand
                    {
                        GameId = gameId,
                        Player = player,
                        CustomUis = false,
                        Templates = true,
                        Flat = false
                    });
                    return new WebRTCApiResult(200, cards);
                }

                case "GET api/materials/getcards":
                {
                    var (_, cards) = await _mediator.Send(new GetAllCardsCommand
                    {
                        GameId = gameId,
                        Player = player,
                        CustomUis = false,
                        Templates = false,
                        Flat = true
                    });
                    return new WebRTCApiResult(200, cards);
                }

                case "GET api/materials/getcard":
                {
                    Guid.TryParse(request.Query.Get("id"), out var id);
                    var result = await _mediator.Send(new GetCardCommand { GameID = gameId, Player = player, Id = id });
                    return new WebRTCApiResult(200, result);
                }

                case "GET api/materials/getcustomviews":
                case "GET api/materials/getcustomwiews": // legacy typo alias, preserved from the real controller
                {
                    var (_, views) = await _mediator.Send(new GetAllCardsCommand
                    {
                        GameId = gameId,
                        Player = player,
                        Templates = false,
                        CustomUis = true
                    });
                    return new WebRTCApiResult(200, views);
                }

                case "POST api/materials/addresource":
                {
                    var body = request.Body;
                    var data = body?["data"]?.Value<string>() ?? string.Empty;
                    // Strip an optional data:<mime>;base64, prefix — same regex the real controller uses.
                    data = new Regex(@"data:(?<type>\w+/\w+);base64,").Replace(data, "", 1);

                    var command = new AddResourceCommand
                    {
                        Name = body?["name"]?.Value<string>(),
                        MimeType = body?["mimeType"]?.Value<string>(),
                        Key = body?["key"]?.Value<string>(),
                        ParentFolder = Guid.TryParse(body?["parentFolder"]?.Value<string>(), out var pf) ? pf : (Guid?)null,
                        GameID = gameId,
                        Data = data,
                        Player = player,
                    };

                    var (_, id) = await _mediator.Send(command);

                    // Original notifies the calling user's connections across all their games via
                    // ILobbyService.SendCommandToUser — unavailable/non-portable here (see plan).
                    // OnlyToSender achieves the same practical effect within this lobby.
                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        await lobby.HandlePostCommand(player, new WebSocketCommand { Command = "resource_notify", OnlyToSender = true });

                    return new WebRTCApiResult(200, new { Id = id });
                }

                case "DELETE api/materials/removeresource":
                {
                    var id = request.Body?.ToObject<Guid?>();
                    var result = await _mediator.Send(new RemoveResourceCommand { ID = id, Player = player, GameId = gameId });
                    return new WebRTCApiResult(200, result);
                }

                case "POST api/materials/createresource":
                {
                    var body = request.Body;
                    var key = body?["key"]?.Value<string>();
                    var name = body?["name"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(name))
                        return new WebRTCApiResult(400, new { error = "key or name is required." });

                    var content = body?["content"]?.Value<string>();
                    byte[] data;
                    try
                    {
                        data = string.IsNullOrEmpty(content) ? Array.Empty<byte>() : Convert.FromBase64String(content);
                    }
                    catch
                    {
                        return new WebRTCApiResult(400, new { error = "content must be a valid base64 string." });
                    }

                    var mimeType = body?["mimeType"]?.Value<string>();
                    var storageKind = body?["storageKind"]?.ToObject<ResourceStorageKind?>() ?? ResourceStorageKind.Blob;

                    var (resp, id) = await _mediator.Send(new CreateResourceCommand
                    {
                        GameId = gameId,
                        Player = player,
                        Key = key,
                        Name = name ?? key ?? string.Empty,
                        Data = data,
                        MimeType = mimeType,
                        StorageKind = storageKind,
                    });

                    if (resp == CommandResponse.AlreadyExists)
                        return new WebRTCApiResult(409, new { error = $"Resource with key '{key}' already exists." });

                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                    {
                        await lobby.HandlePostCommand(player, new WebSocketCommand
                        {
                            Command = WebSocketCommandNames.ResourceAdd,
                            Result = WebSocketCommandNames.ResultOk,
                            Data = JToken.FromObject(new
                            {
                                id,
                                name = name ?? key,
                                path = (string?)null,
                                mimeType,
                                playerId = player.Id,
                                playerName = player.Name,
                            }),
                        });
                    }

                    return new WebRTCApiResult(200, new { id });
                }

                case "PUT api/materials/resourcedata":
                {
                    var body = request.Body;
                    var key = body?["key"]?.Value<string>();
                    var id = Guid.TryParse(body?["id"]?.Value<string>(), out var g) ? g : (Guid?)null;
                    if (string.IsNullOrWhiteSpace(key) && id == null)
                        return new WebRTCApiResult(400, new { error = "key or id is required." });

                    var (_, updatedId) = await _mediator.Send(new UpdateResourceDataCommand
                    {
                        GameId = gameId,
                        Player = player,
                        Key = key,
                        Id = id,
                        Content = body?["content"]?.Value<string>(),
                        MimeType = body?["mimeType"]?.Value<string>(),
                    });

                    return new WebRTCApiResult(200, new { id = updatedId });
                }

                case "DELETE api/materials/resourcedata":
                {
                    var key = request.Query.Get("key");
                    var id = Guid.TryParse(request.Query.Get("id"), out var g) ? g : (Guid?)null;
                    if (string.IsNullOrWhiteSpace(key) && id == null)
                        return new WebRTCApiResult(400, new { error = "key or id query parameter is required." });

                    await _mediator.Send(new DeleteResourceDataCommand { GameId = gameId, Player = player, Key = key, Id = id });
                    return new WebRTCApiResult(200, null);
                }

                default:
                    return null;
            }
        }
    }
}

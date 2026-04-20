using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Card.GetAllCards;
// GetGameListCommand lives in the BattleMap namespace (see its source file)
using DndOnePlaceManager.Application.Commands.Card.GetCard;
using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayout;
using DndOnePlaceManager.Application.Commands.Layouts.GetLayouts;
using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Properties.UpdateProperties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DndOnePlaceManager.Application.Commands.Addons.GetAddons;
using DndOnePlaceManager.Application.Commands.Addons.GetAddonsFromRepository;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Addons.SetAddonEnabled;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Actions.GetActions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Models;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Routes WebRTC API request messages to MediatR commands, bypassing HTTP.
    /// Each registered route maps a "METHOD:path" key (case-insensitive) to a handler
    /// that receives a <see cref="DispatchContext"/> and returns (statusCode, responseBody).
    /// </summary>
    public class WebRTCApiDispatcher : IWebRTCApiDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly Dictionary<string, Func<DispatchContext, Task<(int status, object? body)>>> _routes = new(
            StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<WebRTCApiDispatcher> _logger;

        public WebRTCApiDispatcher(IServiceScopeFactory scopeFactory, ILogger<WebRTCApiDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            RegisterRoutes();
        }

        // ── Public entry point ───────────────────────────────────────────────────

        public async Task DispatchAsync(
            WebRTCApiRequest request,
            PlayerDTO player,
            Guid gameId,
            IPlayerConnection connection)
        {
            var key = BuildKey(request.Method, request.Path);
            (int status, object? body) result;

            _logger.LogDebug("WebRTC API {Method} {Path} requestId={Id} player={Player} game={Game}",
                request.Method, request.Path, request.Id, player.Name, gameId);

            if (_routes.TryGetValue(key, out var handler))
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var ctx = new DispatchContext
                {
                    Player = player,
                    GameId = gameId,
                    Mediator = mediator,
                    Scope = scope.ServiceProvider,
                    Query = request.Query ?? new Dictionary<string, string>(),
                    Body = request.Body
                };

                try
                {
                    result = await handler(ctx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "WebRTC API exception {Method} {Path} requestId={Id} player={Player} game={Game}",
                        request.Method, request.Path, request.Id, player.Name, gameId);
                    result = (500, new { error = ex.Message });
                }
            }
            else
            {
                _logger.LogWarning("No WebRTC route for {Method} {Path} requestId={Id} player={Player}",
                    request.Method, request.Path, request.Id, player.Name);
                result = (404, new { error = $"No WebRTC route for {request.Method} {request.Path}" });
            }

            _logger.LogDebug("WebRTC API {Method} {Path} → {Status} requestId={Id}",
                request.Method, request.Path, result.status, request.Id);

            await connection.SendMessageToPlayer(new
            {
                type = "api-response",
                id = request.Id,
                status = result.status,
                body = result.body
            });
        }

        // ── Route registration ───────────────────────────────────────────────────

        private void RegisterRoutes()
        {
            // ── BattleMap ────────────────────────────────────────────────────────

            Route("GET", "api/battlemap/getplayer", ctx =>
                Task.FromResult<(int, object?)>((200, ctx.Player)));

            Route("GET", "api/battlemap/getfullgame", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetGameCommand
                {
                    GameID = ctx.GameId,
                    PlayerID = ctx.Player.Id ?? default
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getchat", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetMessagesCommand
                {
                    GameID = ctx.GameId,
                    PlayerID = ctx.Player.Id ?? default,
                    Size = ctx.QInt("size", 30),
                    Page = ctx.QInt("page", 0),
                    Filter = ctx.QStr("filter"),
                    From = ctx.Q("from")
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getlayout", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetLayoutCommand
                {
                    Id = ctx.Q("id"),
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getbattlemap", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetBattleMapCommand
                {
                    Id = ctx.Q("id"),
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getbattlemaps", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetBattleMapsCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getlayouts", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetLayoutsCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    Flat = true
                });
                return (200, result);
            });

            Route("GET", "api/battlemap/getcards", async ctx =>
            {
                var (_, cards) = await ctx.Mediator.Send(new GetAllCardsCommand
                {
                    Player = ctx.Player,
                    Templates = false,
                    CustomUis = false
                });
                return (200, cards);
            });

            Route("GET", "api/battlemap/gettree", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetTreeEntriesCommand
                {
                    EntityType = ctx.QStr("entityType"),
                    GameId = ctx.GameId,
                    PlayerId = ctx.Player.Id
                });
                return (200, result);
            });

            // ── Map ──────────────────────────────────────────────────────────────

            Route("GET", "api/map/get", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetMapCommand
                {
                    Id = ctx.Q("mapId"),
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("GET", "api/map/getallflat", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetFlatMapsCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player
                });
                return (200, result);
            });

            // ── Materials ────────────────────────────────────────────────────────

            Route("GET", "api/materials/resource", async ctx =>
            {
                var (data, mime) = await ctx.Mediator.Send(new GetResourceDataCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    ID = ctx.QNullable("id"),
                    Key = ctx.QStr("key", null)
                });
                if (data == null)
                    return (404, new { error = "Resource not found" });
                return (200, new { data = Convert.ToBase64String(data), mimeType = mime.ToString() });
            });

            Route("GET", "api/materials/resourcemetadata", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetResourceCommand
                {
                    Player = ctx.Player,
                    ResourceId = ctx.QNullable("id"),
                    Key = ctx.QStr("key", null)
                });
                return (200, result);
            });

            Route("GET", "api/materials/gettemplates", async ctx =>
            {
                var (_, result) = await ctx.Mediator.Send(new GetAllCardsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Templates = true,
                    CustomUis = false,
                    Flat = true
                });
                return (200, result);
            });

            Route("GET", "api/materials/gettemplatesfull", async ctx =>
            {
                var (_, result) = await ctx.Mediator.Send(new GetAllCardsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Templates = true,
                    CustomUis = false,
                    Flat = false
                });
                return (200, result);
            });

            Route("GET", "api/materials/getcards", async ctx =>
            {
                var (_, result) = await ctx.Mediator.Send(new GetAllCardsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Templates = false,
                    CustomUis = false
                });
                return (200, result);
            });

            Route("GET", "api/materials/getcustomwiews", async ctx =>
            {
                var (_, result) = await ctx.Mediator.Send(new GetAllCardsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Templates = false,
                    CustomUis = true
                });
                return (200, result);
            });

            Route("GET", "api/materials/getcard", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetCardCommand
                {
                    Id = ctx.Q("id"),
                    GameID = ctx.GameId,
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("GET", "api/materials/getresources", async ctx =>
            {
                var (_, result) = await ctx.Mediator.Send(new GetResourcesCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player
                });
                return (200, result);
            });

            Route("POST", "api/materials/addresource", async ctx =>
            {
                var req = ctx.BodyAs<AddResourceBody>();
                if (req == null)
                    return (400, new { error = "Invalid request body" });

                var data = string.IsNullOrEmpty(req.Data) ? Array.Empty<byte>()
                    : Convert.FromBase64String(System.Text.RegularExpressions.Regex
                        .Replace(req.Data, @"data:\w+/\w+;base64,", ""));

                var (_, id) = await ctx.Mediator.Send(new AddResourceCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    Name = req.Name ?? string.Empty,
                    Data = req.Data ?? string.Empty,
                    MimeType = req.MimeType ?? string.Empty,
                    DataRaw = data,
                    Path = req.Key ?? string.Empty
                });
                return (200, new { id });
            });

            Route("DELETE", "api/materials/removeresource", async ctx =>
            {
                var result = await ctx.Mediator.Send(new RemoveResourceCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    ID = ctx.QNullable("id")
                });
                return (200, new { result = result.ToString() });
            });

            // ── Properties ───────────────────────────────────────────────────────

            Route("GET", "api/properties/getproperties", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetPropertiesCommand
                {
                    Player = ctx.Player,
                    ParentID = ctx.Q("parentId")
                });
                return (200, result);
            });

            Route("GET", "api/properties/queryproperties", async ctx =>
            {
                var cmd = new GetPropertiesByQueryCommand { Player = ctx.Player };

                var parentIds = ctx.QStr("parentIds");
                if (!string.IsNullOrWhiteSpace(parentIds))
                    cmd.ParentIDs = parentIds.Split(',').Select(Guid.Parse).ToArray();

                var ids = ctx.QStr("ids");
                if (!string.IsNullOrWhiteSpace(ids))
                    cmd.Ids = ids.Split(',').Select(Guid.Parse).ToArray();

                var names = ctx.QStr("names");
                if (!string.IsNullOrWhiteSpace(names))
                    cmd.PropertyNames = names.Split(',');

                cmd.Prefix = ctx.QStr("prefix", null);

                var result = await ctx.Mediator.Send(cmd);
                return (200, result);
            });

            Route("POST", "api/properties/addbulk", async ctx =>
            {
                var properties = ctx.BodyAs<PropertyBody[]>();
                if (properties == null)
                    return (400, new { error = "Invalid request body" });

                var dtos = properties.Select(p => new DndOnePlaceManager.Application.DataTransferObjects.Game.PropertyDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Value = p.Value,
                    ParentID = p.ParentId,
                    EntityName = p.EntityName
                }).ToArray();

                var result = await ctx.Mediator.Send(new AddPropertiesCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    Properties = dtos
                });
                return (200, new { result = result.ToString() });
            });

            Route("POST", "api/properties/updatebulk", async ctx =>
            {
                var properties = ctx.BodyAs<PropertyBody[]>();
                if (properties == null)
                    return (400, new { error = "Invalid request body" });

                var dtos = properties.Select(p => new DndOnePlaceManager.Application.DataTransferObjects.Game.PropertyDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Value = p.Value,
                    ParentID = p.ParentId,
                    EntityName = p.EntityName
                }).ToArray();

                var result = await ctx.Mediator.Send(new UpdatePropertiesCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    Properties = dtos
                });
                return (200, new { result = result.ToString() });
            });

            // ── Security ─────────────────────────────────────────────────────────

            Route("GET", "api/security/permissions", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetPermissionsCommand
                {
                    EntityId = ctx.Q("entityId"),
                    Player = ctx.Player
                });
                return (200, result);
            });

            // ── GameList ─────────────────────────────────────────────────────────

            Route("GET", "api/gamelist/getgames", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetGameListCommand
                {
                    UserId = ctx.Player.CentralServerUserId ?? string.Empty
                });
                return (200, result);
            });

            // ── Addons ───────────────────────────────────────────────────────────

            Route("GET", "api/addon/installed", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetAddonsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Flat = true
                });
                return (200, result);
            });

            Route("GET", "api/addon/repository", async ctx =>
            {
                var result = await ctx.Mediator.Send(new GetAddonsFromRepositoryCommand());
                return (200, result);
            });

            Route("POST", "api/addon/install", async ctx =>
            {
                var req = ctx.BodyAs<AddonKeyBody>();
                if (string.IsNullOrWhiteSpace(req?.Key))
                    return (400, new { error = "key is required." });

                var (resp, _) = await ctx.Mediator.Send(new InstallAddonCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    AddonSourceKey = req.Key
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, null);
            });

            Route("POST", "api/addon/update", async ctx =>
            {
                var req = ctx.BodyAs<AddonKeyBody>();
                if (string.IsNullOrWhiteSpace(req?.Key))
                    return (400, new { error = "key is required." });

                var (resp, _) = await ctx.Mediator.Send(new InstallAddonCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    AddonSourceKey = req.Key,
                    Reinstall = true
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, null);
            });

            Route("POST", "api/addon/uninstall", async ctx =>
            {
                var req = ctx.BodyAs<AddonIdBody>();
                if (string.IsNullOrWhiteSpace(req?.AddonId))
                    return (400, new { error = "addonId is required." });

                Guid? addonGuid = Guid.TryParse(req.AddonId, out var g) ? g : null;

                var resp = await ctx.Mediator.Send(new UninstallAddonCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    AddonId = addonGuid,
                    AddonKey = addonGuid == null ? req.AddonId : null
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, null);
            });

            Route("POST", "api/addon/setEnabled", async ctx =>
            {
                var req = ctx.BodyAs<SetEnabledBody>();
                if (string.IsNullOrWhiteSpace(req?.AddonId))
                    return (400, new { error = "addonId is required." });

                var resp = await ctx.Mediator.Send(new SetAddonEnabledCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    AddonId = req.AddonId,
                    Enabled = req.Enabled
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, null);
            });

            Route("POST", "api/addon/installFromFile", async ctx =>
            {
                var req = ctx.BodyAs<InstallFromFileBody>();
                if (string.IsNullOrWhiteSpace(req?.Data))
                    return (400, new { error = "data is required." });

                byte[] fileBytes;
                try { fileBytes = Convert.FromBase64String(req.Data); }
                catch { return (400, new { error = "data must be a valid base64 string." }); }

                var (resp, _) = await ctx.Mediator.Send(new InstallAddonCommand
                {
                    GameID = ctx.GameId,
                    Player = ctx.Player,
                    AddonFile = fileBytes,
                    AddonFileName = req.FileName
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, null);
            });

            // ── Actions ──────────────────────────────────────────────────────────

            Route("GET", "api/addon/actions", async ctx =>
            {
                var (resp, dto) = await ctx.Mediator.Send(new GetActionsCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    flatList = true
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                return (200, dto);
            });

            Route("GET", "api/addon/action", async ctx =>
            {
                var (resp, dto) = await ctx.Mediator.Send(new GetActionByIdCommand
                {
                    GameId = ctx.GameId,
                    Player = ctx.Player,
                    Id = ctx.Q("id")
                });
                if (resp != DndOnePlaceManager.Domain.Enums.CommandResponse.Ok)
                    return (400, new { error = resp.ToString() });
                if (dto == null)
                    return (404, new { error = "Action not found." });
                return (200, dto);
            });

            Route("GET", "api/addon/stepdefinitions", ctx =>
            {
                var services = ctx.Scope.GetServices<IActionStepDefinition>();
                var result = new GetActionsDefinitionaResponse
                {
                    StepDefinitions = services.Select(x => new ActionDefinitionResponse
                    {
                        Name = x.Name,
                        Value = x.Value,
                        Category = x.Category,
                        Description = x.Description,
                        Arguments = x.DataType?
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(y => y.SetMethod?.IsPublic == true)
                            .Select(y => new ActionDefinitionArgument
                            {
                                Name = y.Name,
                                Type = y.PropertyType.Name,
                                Description = y.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description
                            }).ToArray()
                    }).ToArray()
                };
                return Task.FromResult<(int, object?)>((200, result));
            });

            Route("GET", "api/addon/hooks", ctx =>
            {
                var hooks = Enum.GetValues(typeof(Hook))
                    .Cast<Hook>()
                    .Select(x => new { Name = x.ToString(), Value = (int)x })
                    .ToArray();
                return Task.FromResult<(int, object?)>((200, hooks));
            });

            Route("GET", "api/addon/runningactions", ctx =>
            {
                var lobbyService = ctx.Scope.GetRequiredService<ILobbyService>();
                var lobby = lobbyService.GetLobby(ctx.GameId);
                if (lobby == null)
                    return Task.FromResult<(int, object?)>((404, (object?)new { error = "Game lobby not found." }));

                var entries = lobby.ActionProcessingService.RunningActions.Values.Select(x => new
                {
                    x.RunId,
                    x.ActionName,
                    x.StartedAt,
                    x.FinishedAt,
                    State = x.State.ToString(),
                    x.CurrentStep,
                    x.FaultMessage,
                    x.WaitingOnToken
                }).ToArray();
                return Task.FromResult<(int, object?)>((200, (object?)entries));
            });
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void Route(string method, string path,
            Func<DispatchContext, Task<(int, object?)>> handler)
        {
            _routes[BuildKey(method, path)] = handler;
        }

        private static string BuildKey(string method, string path)
        {
            var normalised = path.TrimStart('/').TrimEnd('/').ToLowerInvariant();
            return $"{method.ToUpperInvariant()}:{normalised}";
        }

        // ── Nested types ─────────────────────────────────────────────────────────

        /// <summary>Carries per-request state into each route handler.</summary>
        private class DispatchContext
        {
            public PlayerDTO Player { get; init; } = null!;
            public Guid GameId { get; init; }
            public IMediator Mediator { get; init; } = null!;
            public IServiceProvider Scope { get; init; } = null!;
            public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>();
            public JToken? Body { get; init; }

            public Guid Q(string name, Guid fallback = default)
                => Query.TryGetValue(name, out var v) && Guid.TryParse(v, out var g) ? g : fallback;

            public Guid? QNullable(string name)
            {
                if (!Query.TryGetValue(name, out var v)) return null;
                return Guid.TryParse(v, out var g) ? g : null;
            }

            public int QInt(string name, int fallback = 0)
                => Query.TryGetValue(name, out var v) && int.TryParse(v, out var i) ? i : fallback;

            public string QStr(string name, string? fallback = "")
                => Query.TryGetValue(name, out var v) ? v : fallback ?? string.Empty;

            public T? BodyAs<T>() where T : class
            {
                if (Body == null) return null;
                try { return Body.ToObject<T>(); }
                catch { return null; }
            }
        }

        // Minimal POCOs for body deserialization (avoids referencing controller-layer request types)
        private class AddResourceBody
        {
            public string? Name { get; set; }
            public string? MimeType { get; set; }
            public string? Key { get; set; }
            public Guid? ParentFolder { get; set; }
            public string? Data { get; set; }
        }

        private class PropertyBody
        {
            public Guid Id { get; set; }
            public string? Name { get; set; }
            public string? Value { get; set; }
            public Guid ParentId { get; set; }
            public string? EntityName { get; set; }
        }

        private class AddonKeyBody { public string? Key { get; set; } }
        private class AddonIdBody { public string? AddonId { get; set; } }
        private class SetEnabledBody { public string? AddonId { get; set; } public bool Enabled { get; set; } }
        private class InstallFromFileBody { public string? FileName { get; set; } public string? Data { get; set; } public string? MimeType { get; set; } }
    }
}

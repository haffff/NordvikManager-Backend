using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class PropertiesHandler : IWebSocketHandler
    {
        // Matches every other WS handler that rebuilds parsedMsg.Data for broadcast
        // (see TreeHandler.cs) — Newtonsoft has no ambient camelCase default in this
        // codebase, so casing has to be chosen explicitly at each call site or PascalCase
        // C# field names (e.g. ParentID) leak onto the wire unchanged.
        private static readonly JsonSerializer _camelSerializer = new JsonSerializer
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private IMediator mediator;

        public PropertiesHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case WebSocketCommandNames.PropertyUpdate:
                    return await UpdateProperty(parsedMsg, player);
                case WebSocketCommandNames.PropertyRemove:
                    return await RemoveProperty(parsedMsg, player);
                case WebSocketCommandNames.PropertyAdd:
                    return await AddProperty(parsedMsg, player);
                case WebSocketCommandNames.PropertyListItemAdd:
                    return await AddPropertyListItem(parsedMsg, player);
                case WebSocketCommandNames.PropertyListItemRemove:
                    return await RemovePropertyListItem(parsedMsg, player);
                case WebSocketCommandNames.PropertyListItemUpdate:
                    return await UpdatePropertyListItem(parsedMsg, player);
                case WebSocketCommandNames.PropertyListReorder:
                    return await ReorderPropertyList(parsedMsg, player);
                case WebSocketCommandNames.CustomLayerAdd:
                    return await AddCustomLayer(parsedMsg, player);
                case WebSocketCommandNames.CustomLayerRemove:
                    return await RemoveCustomLayer(parsedMsg, player);
                case WebSocketCommandNames.CustomLayerMove:
                    return await MoveCustomLayer(parsedMsg, player);
            }
            return null;
        }

        // In all three cases below, parsedMsg.Data is replaced with a freshly serialized,
        // server-authoritative PropertyDTO before returning — the caller broadcasts
        // parsedMsg as-is, so this is what every connected client actually receives.
        // Previously this re-broadcast whatever Data the client originally sent (or, for
        // Remove, a bare property ID), so the wire shape — including field casing — varied
        // by which code path produced the original message instead of being consistent.

        private async Task<CommandResponse?> AddProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var dto = parsedMsg.Data.ToObject<PropertyDTO>();

            AddPropertyCommand addPropertyCommand = new AddPropertyCommand()
            {
                Player = player,
                Property = dto,
            };

            var (response, added) = await mediator.Send(addPropertyCommand);
            if (added != null)
                parsedMsg.Data = JObject.FromObject(added, _camelSerializer);
            return response;
        }

        private async Task<CommandResponse?> RemoveProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemovePropertyCommand removePropertyCommand = new RemovePropertyCommand()
            {
                Player = player,
                Id = parsedMsg.Data.ToGuid(),
            };

            var (response, removed) = await mediator.Send(removePropertyCommand);
            if (removed != null)
                parsedMsg.Data = JObject.FromObject(removed, _camelSerializer);
            return response;
        }
        private async Task<CommandResponse?> UpdateProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdatePropertyCommand updatePropertyCommand = new UpdatePropertyCommand()
            {
                Player = player,
                Property = parsedMsg.Data.ToObject<PropertyDTO>(),
            };

            var (response, updated) = await mediator.Send(updatePropertyCommand);
            if (updated != null)
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            return response;
        }

        // The four list-op cases below all rewrite parsedMsg.Command to PropertyUpdate
        // before returning, in addition to replacing Data with the canonical PropertyDTO —
        // so every connected client's existing property_update handling (cache refresh,
        // CardAPI subscriptions) picks up list mutations with zero new listening code.

        // Fields are pulled off parsedMsg.Data individually (rather than
        // parsedMsg.Data.ToObject<TCommand>()) so client JSON never binds directly onto
        // a CommandBase-derived type — CommandBase.Scope is server-only and must never
        // be settable from client-controlled input.

        private async Task<CommandResponse?> AddPropertyListItem(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new AddPropertyListItemCommand
            {
                Player = player,
                PropertyId = parsedMsg.Data["propertyId"].ToGuid(),
                Fields = parsedMsg.Data["fields"]?.ToObject<Dictionary<string, string?>>() ?? new(),
                ItemId = parsedMsg.Data["itemId"]?.ToString(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        private async Task<CommandResponse?> RemovePropertyListItem(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new RemovePropertyListItemCommand
            {
                Player = player,
                PropertyId = parsedMsg.Data["propertyId"].ToGuid(),
                ItemId = parsedMsg.Data["itemId"]?.ToString(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        private async Task<CommandResponse?> UpdatePropertyListItem(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new UpdatePropertyListItemCommand
            {
                Player = player,
                PropertyId = parsedMsg.Data["propertyId"].ToGuid(),
                ItemId = parsedMsg.Data["itemId"]?.ToString(),
                Fields = parsedMsg.Data["fields"]?.ToObject<Dictionary<string, string?>>() ?? new(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        private async Task<CommandResponse?> ReorderPropertyList(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new ReorderPropertyListCommand
            {
                Player = player,
                PropertyId = parsedMsg.Data["propertyId"].ToGuid(),
                OrderedItemIds = parsedMsg.Data["orderedItemIds"]?.ToObject<List<string>>() ?? new(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        // GameId is taken from parsedMsg.GameId (server-attached to every WS message
        // on receipt), never from client-supplied Data — same rule as every other
        // game-scoped command in this handler family.
        private async Task<CommandResponse?> AddCustomLayer(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new AddCustomLayerCommand
            {
                Player = player,
                GameId = parsedMsg.GameId ?? default,
                Name = parsedMsg.Data["name"]?.ToString(),
                AfterLayerId = parsedMsg.Data["afterLayerId"]?.ToObject<int?>(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        private async Task<CommandResponse?> RemoveCustomLayer(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new RemoveCustomLayerCommand
            {
                Player = player,
                GameId = parsedMsg.GameId ?? default,
                ItemId = parsedMsg.Data["itemId"]?.ToString(),
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }

        private async Task<CommandResponse?> MoveCustomLayer(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var command = new MoveCustomLayerCommand
            {
                Player = player,
                GameId = parsedMsg.GameId ?? default,
                ItemId = parsedMsg.Data["itemId"]?.ToString(),
                Direction = parsedMsg.Data["direction"]?.ToObject<int>() ?? 0,
            };

            var (response, updated) = await mediator.Send(command);
            if (updated != null)
            {
                parsedMsg.Command = WebSocketCommandNames.PropertyUpdate;
                parsedMsg.Data = JObject.FromObject(updated, _camelSerializer);
            }
            return response;
        }
    }
}

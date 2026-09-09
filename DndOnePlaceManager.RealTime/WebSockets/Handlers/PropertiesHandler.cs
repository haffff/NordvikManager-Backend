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
    }
}

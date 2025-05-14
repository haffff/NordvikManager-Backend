using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class PropertiesHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public PropertiesHandler()
        {
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
                    var (response, id) = await AddProperty(parsedMsg, player);
                    parsedMsg.Data["id"] = id;
                    return response;
            }
            return null;
        }

        private async Task<(CommandResponse, Guid)> AddProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            var dto = parsedMsg.Data.ToObject<PropertyDTO>();

            AddPropertyCommand addPropertyCommand = new AddPropertyCommand()
            {
                Player = player,
                Property = dto,
            };

            return await mediator.Send(addPropertyCommand);
        }

        private async Task<CommandResponse?> RemoveProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemovePropertyCommand removePropertyCommand = new RemovePropertyCommand()
            {
                Player = player,
                Id = parsedMsg.Data.ToGuid(),
            };

            return await mediator.Send(removePropertyCommand);
        }

        private async Task<CommandResponse?> UpdateProperty(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdatePropertyCommand updatePropertyCommand = new UpdatePropertyCommand()
            {
                Player = player,
                Property = parsedMsg.Data.ToObject<PropertyDTO>(),
            };

            return await mediator.Send(updatePropertyCommand);
        }
    }
}

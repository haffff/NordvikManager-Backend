using DndOnePlaceManager.Application.Commands.Security.SetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class PermissionsHandler : IWebSocketHandler
    {
        private IMediator mediator;
        
        public PermissionsHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }


        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.PermissionsUpdate => await SetPermissions(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse?> SetPermissions(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            SetPermissionsCommand setPermissionsCommand = new SetPermissionsCommand()
            {
                EntityId = parsedMsg.Data["id"].ToGuid(),
                EntityType = parsedMsg.Data["entityType"].ToString(),
                GameID = parsedMsg.GameId ?? default,
                Player = player,
                Permissions = parsedMsg.Data["permissions"].ToObject<Dictionary<Guid, Permission?>>()
            };
            return await mediator.Send(setPermissionsCommand);
        }
    }
}

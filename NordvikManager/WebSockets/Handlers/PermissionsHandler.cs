using NordvikManager.Application.Commands.Security.SetPermissions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Extensions;
using NordvikManager.WebSockets.Core;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
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

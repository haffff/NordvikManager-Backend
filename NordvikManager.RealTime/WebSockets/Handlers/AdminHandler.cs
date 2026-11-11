using NordvikManager.Application.Commands.Player.RemovePlayer;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Extensions;
using NordvikManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
{
    public class AdminHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public AdminHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.PlayerKick => await RemovePlayer(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse?> RemovePlayer(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemovePlayerCommand removePlayerCommand = new RemovePlayerCommand()
            {
                Player = player,
                GameID = parsedMsg.GameId ?? default,
                PlayerID = parsedMsg.Data.ToGuid()
            };

            return await mediator.Send(removePlayerCommand);
        }
    }
}

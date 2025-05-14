using DndOnePlaceManager.Application.Commands.Player.RemovePlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
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

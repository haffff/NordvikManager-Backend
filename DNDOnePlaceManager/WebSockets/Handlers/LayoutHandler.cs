using DndOnePlaceManager.Application.Commands.Layouts.AddLayout;
using DndOnePlaceManager.Application.Commands.Layouts.RemoveLayout;
using DndOnePlaceManager.Application.Commands.Layouts.UpdateLayout;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class LayoutHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public LayoutHandler()
        {
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.LayoutAdd => (CommandResponse?)await AddNewLayout(parsedMsg, player),
                WebSocketCommandNames.LayoutUpdate => (CommandResponse?)await UpdateLayout(parsedMsg, player),
                WebSocketCommandNames.LayoutRemove => (CommandResponse?)await RemoveLayout(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse> RemoveLayout(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemoveLayoutCommand removeLayoutCommand = new RemoveLayoutCommand();
            removeLayoutCommand.Player = player;
            removeLayoutCommand.Id = Guid.Parse(parsedMsg.Data.ToString());

            return await mediator.Send(removeLayoutCommand);
        }

        private async Task<CommandResponse> UpdateLayout(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateLayoutCommand cmd = new UpdateLayoutCommand();
            cmd.Player = player;
            cmd.Dto = parsedMsg.Data.ToObject<LayoutDTO>();
            parsedMsg.Data["permission"] = null;
            return await mediator.Send(cmd);
        }

        private async Task<CommandResponse> AddNewLayout(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            AddLayoutCommand cmd = new AddLayoutCommand();
            cmd.Dto = parsedMsg.Data.ToObject<LayoutDTO>();
            cmd.Player = player;
            cmd.GameID = (Guid)parsedMsg.GameId;

            (var result, var id) = await mediator.Send(cmd);
            parsedMsg.Data["id"] = id;
            return result;
        }
    }
}

using NordvikManager.Application.Commands.Layouts.AddLayout;
using NordvikManager.Application.Commands.Layouts.ForceLayout;
using NordvikManager.Application.Commands.Layouts.RemoveLayout;
using NordvikManager.Application.Commands.Layouts.UpdateLayout;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
{
    public class LayoutHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public LayoutHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.LayoutAdd => (CommandResponse?)await AddNewLayout(parsedMsg, player),
                WebSocketCommandNames.LayoutUpdate => (CommandResponse?)await UpdateLayout(parsedMsg, player),
                WebSocketCommandNames.LayoutRemove => (CommandResponse?)await RemoveLayout(parsedMsg, player),
                WebSocketCommandNames.LayoutForceChange => (CommandResponse?)await ForceLayout(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse> ForceLayout(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            ForceLayoutCommand cmd = new ForceLayoutCommand();
            cmd.Player = player;
            cmd.GameID = (Guid)parsedMsg.GameId;
            cmd.LayoutId = Guid.Parse(parsedMsg.Data.ToString());

            return await mediator.Send(cmd);
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

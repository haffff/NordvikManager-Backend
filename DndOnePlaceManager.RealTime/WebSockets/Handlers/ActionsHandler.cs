using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class ActionsHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public ActionsHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case WebSocketCommandNames.ActionUpdate:
                    parsedMsg.OnlyToSender = true;
                    return await UpdateAction(parsedMsg, player);
                case WebSocketCommandNames.ActionRemove:
                case "action_delete":
                    parsedMsg.OnlyToSender = true;
                    return await DeleteAction(parsedMsg, player);
                case WebSocketCommandNames.ActionAdd:
                    parsedMsg.OnlyToSender = true;
                    var (result, guid) = await AddAction(parsedMsg, player);
                    parsedMsg.Data["id"] = guid.ToString();
                    return result;
                default:
                    return null;
            }
        }

        private async Task<(CommandResponse?, Guid?)> AddAction(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            AddActionCommand addActionCommand = new AddActionCommand()
            {
                Action = parsedMsg.Data.ToObject<ActionDto>(),
                Player = player,
                GameId = parsedMsg.GameId ?? default,
            };

            return await mediator.Send(addActionCommand);
        }

        private async Task<CommandResponse?> DeleteAction(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemoveActionCommand removeActionCommand = new RemoveActionCommand()
            {
                Id = parsedMsg.Data.ToGuid(),
                Player = player,
                GameID = parsedMsg.GameId ?? default,
            };

            return await mediator.Send(removeActionCommand);
        }

        private async Task<CommandResponse?> UpdateAction(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateActionCommand updateActionCommand = new UpdateActionCommand()
            {
                Action = parsedMsg.Data.ToObject<ActionDto>(),
                Player = player,
                GameId = parsedMsg.GameId ?? default,
            };

            return await mediator.Send(updateActionCommand);
        }
    }
}

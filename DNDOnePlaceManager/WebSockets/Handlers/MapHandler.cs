using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Map.RemoveMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class MapHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public MapHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            switch (parsedMsg.Command)
            {
                case WebSocketCommandNames.MapChange:
                    return await HandleMapChange(parsedMsg, player);
                case WebSocketCommandNames.MapAdd:
                    (var response, var id) = await HandleMapAdd(parsedMsg, player);
                    parsedMsg.Result = id;
                    return response;
                case WebSocketCommandNames.MapRemove:
                    return await HandleMapRemove(parsedMsg, player);
                default:
                    return null;
            }

            return null;
        }

        private async Task<CommandResponse> HandleMapChange(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateBattleMapCommand updateBattleMapCommand = new UpdateBattleMapCommand()
            {
                Dto = parsedMsg.Data.ToObject<BattleMapDto>(),
                Player = player,
            };

            var result = await mediator.Send(updateBattleMapCommand);

            return result;
        }

        private async Task<(CommandResponse, Guid)> HandleMapAdd(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            AddMapCommand addMapCmd = new AddMapCommand()
            {
                Player = player,
                GameID = parsedMsg.GameId ?? default,
            };
            return await mediator.Send(addMapCmd);
        }

        private async Task<CommandResponse> HandleMapRemove(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            RemoveMapCommand removeMapCmd = new RemoveMapCommand()
            {
                GameID = parsedMsg.GameId ?? default,
                Id = parsedMsg.Data.ToGuid(),
                Player = player,
            };
            return await mediator.Send(removeMapCmd);
        }
    }
}

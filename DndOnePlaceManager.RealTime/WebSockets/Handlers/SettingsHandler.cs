using DndOnePlaceManager.Application.Commands.Game.UpdateGame;
using DndOnePlaceManager.Application.Commands.Map.UpdateMap;
using DndOnePlaceManager.Application.Commands.Player.UpdatePlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets.Handlers
{
    public class SettingsHandler : IWebSocketHandler
    {
        private IMediator mediator;

        public SettingsHandler(IMediator mediator)
        {
            this.mediator = mediator;
        }

        public async Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            return parsedMsg.Command switch
            {
                WebSocketCommandNames.SettingsMap => (CommandResponse?)await UpdateMap(parsedMsg, player),
                WebSocketCommandNames.SettingsPlayer => (CommandResponse?)await UpdatePlayer(parsedMsg, player),
                WebSocketCommandNames.SettingsGame => (CommandResponse?)await UpdateGame(parsedMsg, player),
                _ => null,
            };
        }

        private async Task<CommandResponse> UpdateGame(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateGameCommand cmd = new UpdateGameCommand();
            cmd.Player = player;
            cmd.GameId = (Guid)parsedMsg.GameId;
            cmd.Name = parsedMsg.Data["name"]?.Value<string?>();
            cmd.Password = parsedMsg.Data["password"]?.Value<string?>();

            parsedMsg.Data["permission"] = null;

            return await mediator.Send(cmd);
        }

        private async Task<CommandResponse> UpdatePlayer(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdatePlayerCommand cmd = new UpdatePlayerCommand();
            cmd.Player = player;
            cmd.GameId = (Guid)parsedMsg.GameId;

            var playerDTO = parsedMsg.Data.ToObject<PlayerDTO>();
            cmd.playerDTO = playerDTO;

            return await mediator.Send(cmd);
        }

        private async Task<CommandResponse> UpdateMap(WebSocketCommand parsedMsg, PlayerDTO player)
        {
            UpdateMapCommand cmd = new UpdateMapCommand();
            cmd.Player = player;
            cmd.GameId = (Guid)parsedMsg.GameId;

            var mapDTO = parsedMsg.Data.ToObject<MapDTO>();
            cmd.Map = mapDTO;


            return await mediator.Send(cmd);
        }
    }
}

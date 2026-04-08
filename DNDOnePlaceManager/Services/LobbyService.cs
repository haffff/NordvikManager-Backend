using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    public class LobbyService : ILobbyService
    {
        private readonly ILobbyRegistry _lobbyRegistry;

        public LobbyService(ILobbyRegistry lobbyRegistry)
        {
            _lobbyRegistry = lobbyRegistry;
        }

        public GameLobby? GetLobby(Guid gameId)
        {
            _lobbyRegistry.Games.TryGetValue(gameId, out var lobby);
            return lobby;
        }

        public async Task HandleCommandInLobbyAsync(Guid? gameId, WebSocketCommand command, PlayerDTO player)
        {
            if (gameId == null)
                return;
            if (_lobbyRegistry.Games.TryGetValue(gameId.Value, out var lobby))
                await lobby.HandleCommand(player, command);
        }

        public async Task<bool> ExecuteActionInGameLobbyAsync(ActionDto action, Guid gameId)
        {
            await _lobbyRegistry.Games[gameId].ActionProcessingService.ExecActionAsync(
                action, new BackEndHookArgs { GameId = gameId });
            return true;
        }

        public async Task<bool> SendLogInformationAsync(string message, string code, LogLevel logType, Guid gameId, Guid? playerId)
        {
            var game = _lobbyRegistry.Games[gameId];
            var playerEntry = game.ConnectedPlayers.Keys.FirstOrDefault(x => x.Id == playerId);
            if (playerEntry != null)
            {
                foreach (var conn in game.ConnectedPlayers[playerEntry])
                    await conn.SendMessageToPlayer(
                        new { command = WebSocketCommandNames.LogCommand, data = new { message, code, logType } });
            }
            return true;
        }

        public void SendCommandToUser(User user, WebSocketCommand command)
        {
            if (_lobbyRegistry?.Games == null)
                return;
            foreach (var game in _lobbyRegistry.Games.Values)
            {
                var playerEntry = game.ConnectedPlayers.Keys
                    .FirstOrDefault(p => p.CentralServerUserId == user.Id);
                if (playerEntry != null)
                    _ = game.ConnectedPlayers[playerEntry].SendMessageToPlayer(command);
            }
        }
        public void SendKickToPlayer(Guid playerId)
        {
            if (_lobbyRegistry?.Games == null)
                return;

            foreach (var game in _lobbyRegistry.Games.Values)
            {
                var playerEntry = game.ConnectedPlayers.Keys
                    .FirstOrDefault(p => p.Id == playerId);

                if (playerEntry == null)
                    continue;

                var kickCommand = new WebSocketCommand
                {
                    GameId = game.GameId,
                    PlayerId = playerId,
                    Command = WebSocketCommandNames.PlayerKick,
                    OnlyToSender = true
                };

                _ = game.ConnectedPlayers[playerEntry].SendMessageToPlayer(kickCommand);
                game.ConnectedPlayers.Remove(playerEntry);
                return;
            }
        }
    }
}

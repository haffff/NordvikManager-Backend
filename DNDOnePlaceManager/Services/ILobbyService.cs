using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services
{
    public interface ILobbyService
    {
        GameLobby? GetLobby(Guid gameId);
        Task HandleCommandInLobbyAsync(Guid? gameId, WebSocketCommand command, PlayerDTO player);
        Task<bool> ExecuteActionInGameLobbyAsync(ActionDto action, Guid gameId);
        Task<bool> SendLogInformationAsync(string message, string code, LogLevel logType, Guid gameId, Guid? playerId);
        void SendCommandToUser(User user, WebSocketCommand command);
        void SendKickToPlayer(Guid playerId);
    }
}

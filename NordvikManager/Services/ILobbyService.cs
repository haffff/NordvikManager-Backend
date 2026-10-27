using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Services
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

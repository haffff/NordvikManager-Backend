using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebSockets
{
    public interface IWebSocketManager
    {
        Task<bool> ExecuteActionInGameLobby(ActionDto action, Guid gameId);
        Task<bool> SendLogInformation(string message, string code, LogLevel logType , Guid gameId, Guid? id);
        Task Handle(HttpContext httpContext);
        Task HandleCommandInLobby(Guid? gameId, WebSocketCommand command, PlayerDTO player);
    }
}

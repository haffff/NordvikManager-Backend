using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using MediatR;
using System.Threading.Tasks;

namespace NordvikManager.WebSockets.Handlers
{
    public interface IWebSocketHandler
    {
        /// <summary>
        /// Handles category of commands.
        /// </summary>
        /// <param name="parsedMsg">Incoming command</param>
        /// <returns>Additional information that is passed to client</returns>
        public Task<CommandResponse?> Handle(WebSocketCommand parsedMsg, PlayerDTO player);
    }
}
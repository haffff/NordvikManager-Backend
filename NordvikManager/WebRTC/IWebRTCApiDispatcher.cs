using NordvikManager.Application.DataTransferObjects.Game;
using System;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Dispatches incoming WebRTC API requests (tunneled REST calls) to the appropriate
    /// MediatR command handler and writes the response back on the data channel.
    /// </summary>
    public interface IWebRTCApiDispatcher
    {
        Task DispatchAsync(
            WebRTCApiRequest request,
            PlayerDTO player,
            Guid gameId,
            IPlayerConnection connection);
    }
}

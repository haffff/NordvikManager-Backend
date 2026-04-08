using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    public interface IWebRTCSessionService
    {
        /// <summary>Returns true if the WebRTC signaling connection for this game is active.</summary>
        bool IsConnected(Guid gameId);

        /// <summary>Starts a WebRTC session: connects to Central Server signaling and begins handling peer connections.</summary>
        Task StartSessionAsync(Guid gameId, string centralSessionId, string centralToken);

        /// <summary>Stops the WebRTC session and closes all peer connections for a game.</summary>
        Task StopSessionAsync(Guid gameId);
    }
}

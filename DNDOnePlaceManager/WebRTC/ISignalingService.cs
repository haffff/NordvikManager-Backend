using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    public interface ISignalingService : IAsyncDisposable
    {
        /// <summary>Returns true if the Socket.io client for this game is currently connected.</summary>
        bool IsConnected(string gameId);

        /// <summary>Connects to the Central Server session and starts listening for WebRTC events.</summary>
        Task ConnectAsync(string gameId, string centralSessionId, string centralToken);

        /// <summary>Disconnects and cleans up the Socket.io client for a game.</summary>
        Task DisconnectAsync(string gameId);

        /// <summary>Sends a WebRTC answer SDP to a specific browser peer via the Central Server relay.</summary>
        Task SendAnswerAsync(string gameId, string targetSocketId, string sdp);

        /// <summary>Sends an ICE candidate to a specific browser peer via the Central Server relay.</summary>
        Task SendIceCandidateAsync(string gameId, string targetSocketId, string candidate, string? sdpMid, int? sdpMLineIndex);

        event Func<PeerJoinedArgs, Task>? PeerJoined;
        event Func<PeerLeftArgs, Task>? PeerLeft;
        event Func<WebRTCSignalArgs, Task>? OfferReceived;
        event Func<IceCandidateArgs, Task>? IceCandidateReceived;
    }

    public record PeerJoinedArgs(string GameId, string UserId, string Username, string SocketId);
    public record PeerLeftArgs(string GameId, string SocketId);
    public record WebRTCSignalArgs(string GameId, string FromSocketId, string Sdp, string? UserId = null, string? Username = null);
    public record IceCandidateArgs(string GameId, string FromSocketId, string Candidate, string? SdpMid, int? SdpMLineIndex, string? UserId = null);
}

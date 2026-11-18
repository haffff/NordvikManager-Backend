using NordvikManager.WebRTC;

namespace NordvikManager.Wasm.Host.SelfCheck
{
    /// <summary>
    /// Signaling inside the page, for the self-check: the in-page player's offer and candidates
    /// are raised as if Central relayed them, and the GM's answer and candidates go straight
    /// back to it. Stands in for SignalingService (Socket.IO to Central).
    /// </summary>
    internal sealed class InPageSignaling : ISignalingService
    {
        public Func<string, Task>? AnswerSent { get; set; }
        public Action<string, string?, int?>? CandidateSent { get; set; }

        public event Func<PeerJoinedArgs, Task>? PeerJoined;
        public event Func<PeerLeftArgs, Task>? PeerLeft;
        public event Func<WebRTCSignalArgs, Task>? OfferReceived;
        public event Func<IceCandidateArgs, Task>? IceCandidateReceived;

        public bool IsConnected(string gameId) => true;

        public Task ConnectAsync(string gameId, string centralSessionId, string credential) => Task.CompletedTask;

        public Task DisconnectAsync(string gameId) => Task.CompletedTask;

        public Task SendAnswerAsync(string gameId, string targetSocketId, string sdp)
            => AnswerSent?.Invoke(sdp) ?? Task.CompletedTask;

        public Task SendIceCandidateAsync(string gameId, string targetSocketId, string candidate, string? sdpMid, int? sdpMLineIndex)
        {
            CandidateSent?.Invoke(candidate, sdpMid, sdpMLineIndex);
            return Task.CompletedTask;
        }

        public Task RaiseOfferAsync(WebRTCSignalArgs args) => OfferReceived?.Invoke(args) ?? Task.CompletedTask;

        public Task RaiseCandidateAsync(IceCandidateArgs args) => IceCandidateReceived?.Invoke(args) ?? Task.CompletedTask;

        public Task RaisePeerLeftAsync(PeerLeftArgs args) => PeerLeft?.Invoke(args) ?? Task.CompletedTask;

        public Task RaisePeerJoinedAsync(PeerJoinedArgs args) => PeerJoined?.Invoke(args) ?? Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

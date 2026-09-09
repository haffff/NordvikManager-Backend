using DNDOnePlaceManager.WebRTC;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Fake ISignalingService for testing the REAL WebRTCSessionService without a real
    /// Central Server connection — SocketIOClient's wasm-runtime compatibility is a
    /// separate, unresolved spike (see the plan); this sidesteps it entirely. Only the
    /// signaling *transport* is faked here; WebRTCSessionService itself is 100%
    /// production code, unaware it isn't talking to a real Central Server.
    ///
    /// "Peer joined" is raised directly by WebRTCSessionServiceHarness (see
    /// RaisePeerJoinedForTest) rather than round-tripped through JS, since it's a
    /// signaling-layer notification with no corresponding browser/WebRTC event to hang
    /// it off of — a real SignalingService learns about it from the Central Server's
    /// "peer-joined" socket.io event, not from anything WebRTC-related.
    /// Offer/answer/ICE candidate exchange do go through the JS-only "peerA" stand-in
    /// in testSignaling.js (same pattern SessionServiceHarness already uses), via the
    /// JSImport functions below.
    /// </summary>
    public partial class TestSignalingService : ISignalingService
    {
        // JSExport callbacks must be static — this is how they reach the one instance
        // DI ever constructs (registered as a singleton), mirroring the Registry pattern
        // RTCPeerConnection/RTCDataChannel use in the wrapper.
        private static TestSignalingService? _current;

        // Only one simulated peer in this harness — fixed identity, GameId set by
        // WebRTCSessionServiceHarness before StartSessionAsync so the JSExport callback
        // below can attribute ICE candidates to the right game/socket.
        internal static string GameId = string.Empty;
        internal const string SocketId = "test-socket-1";

        public event Func<PeerJoinedArgs, Task>? PeerJoined;
        public event Func<PeerLeftArgs, Task>? PeerLeft;
        public event Func<WebRTCSignalArgs, Task>? OfferReceived;
        public event Func<IceCandidateArgs, Task>? IceCandidateReceived;

        public TestSignalingService()
        {
            _current = this;
        }

        public bool IsConnected(string gameId) => true;
        public Task ConnectAsync(string gameId, string centralSessionId, string centralToken) => Task.CompletedTask;
        public Task DisconnectAsync(string gameId) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task SendAnswerAsync(string gameId, string targetSocketId, string sdp)
            => SubmitAnswer(sdp);

        public Task SendIceCandidateAsync(string gameId, string targetSocketId, string candidate, string? sdpMid, int? sdpMLineIndex)
            => SubmitCandidateFromPeerB(candidate, sdpMid, sdpMLineIndex ?? 0);

        /// <summary>Synthesizes the "a player connected" signaling notification. See class remarks.</summary>
        public Task RaisePeerJoinedForTest(string userId, string username)
            => PeerJoined?.Invoke(new PeerJoinedArgs(GameId, userId, username, SocketId)) ?? Task.CompletedTask;

        public Task RaiseOfferReceivedForTest(string sdp)
            => OfferReceived?.Invoke(new WebRTCSignalArgs(GameId, SocketId, sdp)) ?? Task.CompletedTask;

        [JSImport("createPeerAOffer", "test-signaling")]
        internal static partial Task<string> CreatePeerAOffer();

        [JSImport("sendFromPeerA", "test-signaling")]
        internal static partial Task SendFromPeerA(string text);

        [JSImport("submitAnswer", "test-signaling")]
        private static partial Task SubmitAnswer(string sdp);

        [JSImport("submitCandidateFromPeerB", "test-signaling")]
        private static partial Task SubmitCandidateFromPeerB(string candidate, string? sdpMid, int sdpMLineIndex);

        [JSExport]
        internal static void OnCandidateFromPeerA(string candidate, string? sdpMid, int sdpMLineIndex)
        {
            if (_current?.IceCandidateReceived == null) return;
            _ = _current.IceCandidateReceived.Invoke(
                new IceCandidateArgs(GameId, SocketId, candidate, sdpMid, sdpMLineIndex));
        }

        [JSExport]
        internal static void OnMessageFromPeerB(string text)
        {
            WebRTCSessionServiceHarness.OnMessageObserved(text);
        }
    }
}

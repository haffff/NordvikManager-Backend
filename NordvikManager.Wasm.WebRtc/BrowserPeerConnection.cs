using NordvikManager.WebRTC;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NordvikManager.Wasm.WebRtc
{
    /// <summary>The browser-wasm GM host's WebRTC: the browser's own RTCPeerConnection.</summary>
    public sealed class BrowserPeerConnectionFactory : IPeerConnectionFactory
    {
        private readonly IBrowserRtc _rtc;
        private readonly BrowserRtcRouter _router;

        public BrowserPeerConnectionFactory(IBrowserRtc rtc, BrowserRtcRouter? router = null)
        {
            _rtc = rtc;
            _router = router ?? BrowserRtcRouter.Default;
        }

        public IPeerConnection Create(IReadOnlyList<IceServer> iceServers)
        {
            var peer = new BrowserPeerConnection(_router.NextPeerId(), _rtc, _router);
            // Known before the browser can raise anything for it.
            _router.Add(peer);
            try
            {
                _rtc.CreatePeer(peer.Id, IceServersJson(iceServers));
            }
            catch
            {
                _router.Remove(peer);
                throw;
            }
            return peer;
        }

        // RTCIceServer: { urls, username?, credential? }. Source-generated: browser-wasm turns
        // reflection-based System.Text.Json off by default.
        private static string IceServersJson(IReadOnlyList<IceServer> servers)
            => JsonSerializer.Serialize(servers.Select(s => new RtcIceServer(s.Url, s.Username, s.Credential)).ToArray(),
                BrowserRtcJson.Default.RtcIceServerArray);
    }

    internal sealed record RtcIceServer(
        [property: JsonPropertyName("urls")] string Urls,
        [property: JsonPropertyName("username")] string? Username,
        [property: JsonPropertyName("credential")] string? Credential);

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(RtcIceServer[]))]
    internal sealed partial class BrowserRtcJson : JsonSerializerContext
    {
    }

    internal sealed class BrowserPeerConnection : IPeerConnection
    {
        private readonly IBrowserRtc _rtc;
        private readonly BrowserRtcRouter _router;
        private readonly List<BrowserDataChannel> _channels = new();
        private bool _closed;

        public BrowserPeerConnection(string id, IBrowserRtc rtc, BrowserRtcRouter router)
        {
            Id = id;
            _rtc = rtc;
            _router = router;
        }

        public string Id { get; }

        internal IReadOnlyList<BrowserDataChannel> Channels
        {
            get { lock (_channels) return _channels.ToList(); }
        }

        public PeerState State { get; private set; } = PeerState.New;

        public bool HasRemoteDescription { get; private set; }

        public event Action<PeerState>? StateChanged;
        public event Action<IPeerDataChannel>? DataChannel;
        public event Action<IceCandidate>? LocalCandidate;

        // The player's candidates until its offer is set (then null): the browser's
        // setRemoteDescription is a Promise, and addIceCandidate before it completes fails.
        private readonly object _candidateLock = new();
        private List<IceCandidate>? _pendingCandidates = new();

        public async Task<string?> SetRemoteOfferAsync(string sdp)
        {
            var error = await _rtc.SetRemoteOfferAsync(Id, sdp);
            List<IceCandidate> pending;
            lock (_candidateLock)
            {
                pending = _pendingCandidates ?? new();
                // A rejected offer's candidates have nothing to go to.
                _pendingCandidates = error == null ? null : new();
            }
            if (error != null)
                return error;

            HasRemoteDescription = true;
            foreach (var candidate in pending)
                SendCandidate(candidate);
            return null;
        }

        public Task<string> CreateAnswerAsync() => _rtc.CreateAnswerAsync(Id);

        public void AddRemoteCandidate(IceCandidate candidate)
        {
            lock (_candidateLock)
            {
                if (_pendingCandidates != null)
                {
                    _pendingCandidates.Add(candidate);
                    return;
                }
            }
            SendCandidate(candidate);
        }

        private void SendCandidate(IceCandidate candidate)
            => _rtc.AddIceCandidate(Id, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex ?? -1);

        // The browser raises nothing for a close() of its own, so the channels are closed here:
        // their lobby connections go away as they would natively.
        public void Close()
        {
            if (_closed)
                return;
            _closed = true;
            State = PeerState.Closed;
            _router.Remove(this);
            try
            {
                _rtc.ClosePeer(Id);
            }
            finally
            {
                foreach (var channel in Channels)
                    channel.RaiseClosed();
            }
        }

        internal BrowserDataChannel AddChannel(string channelId, bool isOpen)
        {
            var channel = new BrowserDataChannel(channelId, isOpen, _rtc);
            lock (_channels)
                _channels.Add(channel);
            return channel;
        }

        internal void RaiseState(string state)
        {
            if (ParseState(state) is not { } parsed)
                return;
            State = parsed;
            StateChanged?.Invoke(parsed);
        }

        internal void RaiseLocalCandidate(string? candidate, string? sdpMid, int sdpMLineIndex)
        {
            // A null or empty candidate is the browser's end of gathering: nothing to send.
            if (string.IsNullOrEmpty(candidate))
                return;
            LocalCandidate?.Invoke(new IceCandidate(candidate, sdpMid, sdpMLineIndex < 0 ? null : sdpMLineIndex));
        }

        internal void RaiseDataChannel(BrowserDataChannel channel) => DataChannel?.Invoke(channel);

        private static PeerState? ParseState(string state) => state switch
        {
            "new" => PeerState.New,
            "connecting" => PeerState.Connecting,
            "connected" => PeerState.Connected,
            "disconnected" => PeerState.Disconnected,
            "failed" => PeerState.Failed,
            "closed" => PeerState.Closed,
            _ => null,
        };
    }

    internal sealed class BrowserDataChannel : IPeerDataChannel
    {
        private readonly IBrowserRtc _rtc;
        private int _opened;
        private int _closed;

        public BrowserDataChannel(string id, bool isOpen, IBrowserRtc rtc)
        {
            Id = id;
            _rtc = rtc;
            if (isOpen)
                _opened = 1;
        }

        public string Id { get; }

        public bool IsOpen => _opened == 1 && _closed == 0;

        public ulong BufferedAmount => (ulong)Math.Max(0, _rtc.BufferedAmount(Id));

        public event Action? Opened;
        public event Action? Closed;
        public event Action<byte[]>? Message;

        public void Send(string message) => _rtc.Send(Id, message);

        // Once, as the browser's open event; never for a channel announced already open.
        internal void RaiseOpened()
        {
            if (_closed == 0 && Interlocked.Exchange(ref _opened, 1) == 0)
                Opened?.Invoke();
        }

        internal void RaiseClosed()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
                Closed?.Invoke();
        }

        internal void RaiseMessage(string text) => Message?.Invoke(Encoding.UTF8.GetBytes(text));
    }
}

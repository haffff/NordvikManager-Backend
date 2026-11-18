using System.Collections.Concurrent;

namespace NordvikManager.Wasm.WebRtc
{
    /// <summary>
    /// Takes the browser's WebRTC events (by id) to the peer connection or data channel they
    /// belong to. Events for ids it doesn't know (a peer already closed) are dropped.
    /// <see cref="Default"/> is the one the JS callbacks (<see cref="JsBrowserRtc"/>) use.
    /// </summary>
    public sealed class BrowserRtcRouter
    {
        public static BrowserRtcRouter Default { get; } = new();

        private readonly ConcurrentDictionary<string, BrowserPeerConnection> _peers = new();
        private readonly ConcurrentDictionary<string, BrowserDataChannel> _channels = new();
        private long _nextId;

        internal string NextPeerId() => "pc-" + Interlocked.Increment(ref _nextId);

        internal void Add(BrowserPeerConnection peer) => _peers[peer.Id] = peer;

        internal void Remove(BrowserPeerConnection peer)
        {
            _peers.TryRemove(peer.Id, out _);
            foreach (var channel in peer.Channels)
                _channels.TryRemove(channel.Id, out _);
        }

        public void OnStateChanged(string peerId, string state)
        {
            if (_peers.TryGetValue(peerId, out var peer))
                peer.RaiseState(state);
        }

        public void OnIceCandidate(string peerId, string? candidate, string? sdpMid, int sdpMLineIndex)
        {
            if (_peers.TryGetValue(peerId, out var peer))
                peer.RaiseLocalCandidate(candidate, sdpMid, sdpMLineIndex);
        }

        /// <param name="isOpen">Whether the channel was already open when the browser announced it.</param>
        public void OnDataChannel(string peerId, string channelId, bool isOpen)
        {
            if (!_peers.TryGetValue(peerId, out var peer))
                return;
            var channel = peer.AddChannel(channelId, isOpen);
            _channels[channelId] = channel;
            peer.RaiseDataChannel(channel);
        }

        public void OnChannelOpen(string channelId)
        {
            if (_channels.TryGetValue(channelId, out var channel))
                channel.RaiseOpened();
        }

        public void OnChannelClose(string channelId)
        {
            if (_channels.TryRemove(channelId, out var channel))
                channel.RaiseClosed();
        }

        public void OnChannelMessage(string channelId, string text)
        {
            if (_channels.TryGetValue(channelId, out var channel))
                channel.RaiseMessage(text);
        }
    }
}

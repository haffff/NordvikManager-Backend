namespace NordvikManager.Wasm.WebRtc
{
    /// <summary>
    /// The browser's RTCPeerConnection / RTCDataChannel, by string id: JS interop can't hand
    /// object references to C#. Peer ids are chosen here (so events can't arrive for a peer
    /// C# doesn't know yet), channel ids by the browser. Events come back through
    /// <see cref="BrowserRtcRouter"/>. <see cref="JsBrowserRtc"/> is the real one.
    /// </summary>
    public interface IBrowserRtc
    {
        /// <param name="iceServersJson">An RTCIceServer[] as JSON: urls, and username/credential for TURN.</param>
        void CreatePeer(string peerId, string iceServersJson);

        /// <summary>Null when the browser accepted the offer, otherwise its error.</summary>
        Task<string?> SetRemoteOfferAsync(string peerId, string sdp);

        /// <summary>createAnswer + setLocalDescription; the answer's SDP.</summary>
        Task<string> CreateAnswerAsync(string peerId);

        /// <param name="sdpMLineIndex">-1 for none.</param>
        void AddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex);

        void ClosePeer(string peerId);

        /// <summary>Throws when the channel isn't open (the browser's InvalidStateError).</summary>
        void Send(string channelId, string message);

        double BufferedAmount(string channelId);
    }
}

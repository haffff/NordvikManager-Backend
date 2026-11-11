using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    // The WebRTC a GM host needs, without saying which stack provides it: SIPSorcery in the
    // native server, the browser's own RTCPeerConnection when the host runs as wasm in a tab.
    // Shaped after the browser API (async negotiation), which SIPSorcery fits by returning
    // completed tasks. Nothing here may reference SIPSorcery.

    public enum PeerState { New, Connecting, Connected, Disconnected, Failed, Closed }

    /// <summary>One ICE server URL; TURN ones carry their credentials.</summary>
    public sealed record IceServer(string Url, string? Username = null, string? Credential = null);

    /// <summary>An ICE candidate as browsers write it (with the "candidate:" prefix).</summary>
    public sealed record IceCandidate(string Candidate, string? SdpMid, int? SdpMLineIndex);

    public interface IPeerConnectionFactory
    {
        IPeerConnection Create(IReadOnlyList<IceServer> iceServers);
    }

    /// <summary>The GM side of one player's connection: it answers the player's offer.</summary>
    public interface IPeerConnection
    {
        PeerState State { get; }

        /// <summary>Whether an offer was set, i.e. this connection negotiated.</summary>
        bool HasRemoteDescription { get; }

        event Action<PeerState>? StateChanged;

        /// <summary>A data channel the player opened.</summary>
        event Action<IPeerDataChannel>? DataChannel;

        /// <summary>A candidate of ours, to send to the player.</summary>
        event Action<IceCandidate>? LocalCandidate;

        /// <summary>Null when the offer was accepted, otherwise why not.</summary>
        Task<string?> SetRemoteOfferAsync(string sdp);

        /// <summary>Creates the answer, sets it as the local description and returns its SDP.</summary>
        Task<string> CreateAnswerAsync();

        void AddRemoteCandidate(IceCandidate candidate);

        void Close();
    }

    public interface IPeerDataChannel : IDataChannelSender
    {
        /// <summary>Raised once, when the channel opens; check <see cref="IDataChannelSender.IsOpen"/> for one already open.</summary>
        event Action? Opened;

        event Action? Closed;

        /// <summary>A message, as UTF-8 bytes.</summary>
        event Action<byte[]>? Message;
    }
}

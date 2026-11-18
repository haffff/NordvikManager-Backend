using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>The native server's WebRTC: SIPSorcery behind <see cref="IPeerConnection"/>.</summary>
    public sealed class SipsorceryPeerConnectionFactory : IPeerConnectionFactory
    {
        public IPeerConnection Create(IReadOnlyList<IceServer> iceServers)
            => new SipsorceryPeerConnection(new RTCPeerConnection(new RTCConfiguration
            {
                iceServers = iceServers.Select(ToRtcIceServer).ToList(),
            }));

        private static RTCIceServer ToRtcIceServer(IceServer server) => new()
        {
            urls = server.Url,
            username = server.Username,
            credential = server.Credential,
            credentialType = RTCIceCredentialType.password,
        };
    }

    internal sealed class SipsorceryPeerConnection : IPeerConnection
    {
        private readonly RTCPeerConnection _pc;

        public SipsorceryPeerConnection(RTCPeerConnection pc)
        {
            _pc = pc;
            _pc.onconnectionstatechange += state => StateChanged?.Invoke(ToPeerState(state));
            _pc.ondatachannel += channel => DataChannel?.Invoke(new SipsorceryDataChannel(channel));
            _pc.onicecandidate += candidate =>
            {
                if (candidate != null)
                    LocalCandidate?.Invoke(new IceCandidate(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex));
            };
        }

        public PeerState State => ToPeerState(_pc.connectionState);

        public bool HasRemoteDescription => _pc.remoteDescription != null;

        public event Action<PeerState>? StateChanged;
        public event Action<IPeerDataChannel>? DataChannel;
        public event Action<IceCandidate>? LocalCandidate;

        public Task<string?> SetRemoteOfferAsync(string sdp)
        {
            var result = _pc.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.offer, sdp = sdp });
            return Task.FromResult(result == SetDescriptionResultEnum.OK ? null : result.ToString());
        }

        public async Task<string> CreateAnswerAsync()
        {
            var answer = _pc.createAnswer(null);
            await _pc.setLocalDescription(answer);
            return answer.sdp;
        }

        public void AddRemoteCandidate(IceCandidate candidate)
        {
            // Browsers send candidates with the "candidate:" prefix. SIPSorcery expects the raw
            // candidate string WITHOUT that prefix (it adds its own "a=candidate:" when parsing).
            var text = candidate.Candidate ?? string.Empty;
            if (text.StartsWith("candidate:", StringComparison.OrdinalIgnoreCase))
                text = text.Substring("candidate:".Length);

            _pc.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = text,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex.HasValue ? (ushort)candidate.SdpMLineIndex.Value : (ushort)0,
            });
        }

        public void Close() => _pc.close();

        internal static PeerState ToPeerState(RTCPeerConnectionState state) => state switch
        {
            RTCPeerConnectionState.@new => PeerState.New,
            RTCPeerConnectionState.connecting => PeerState.Connecting,
            RTCPeerConnectionState.connected => PeerState.Connected,
            RTCPeerConnectionState.disconnected => PeerState.Disconnected,
            RTCPeerConnectionState.failed => PeerState.Failed,
            _ => PeerState.Closed,
        };
    }

    internal sealed class SipsorceryDataChannel : IPeerDataChannel
    {
        private readonly RTCDataChannel _channel;

        public SipsorceryDataChannel(RTCDataChannel channel)
        {
            _channel = channel;
            // SIPSorcery fires onopen as a plain Action exactly once.
            _channel.onopen += () => Opened?.Invoke();
            _channel.onclose += () => Closed?.Invoke();
            _channel.onmessage += (_, _, data) => Message?.Invoke(data);
        }

        public bool IsOpen => _channel.readyState == RTCDataChannelState.open;

        public ulong BufferedAmount => _channel.bufferedAmount;

        public event Action? Opened;
        public event Action? Closed;
        public event Action<byte[]>? Message;

        public void Send(string message) => _channel.send(message);
    }
}

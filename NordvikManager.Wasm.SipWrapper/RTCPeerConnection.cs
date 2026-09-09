using System.Collections.Concurrent;
using System.Text.Json;
using NordvikManager.Wasm.SipWrapper.Interop;

namespace NordvikManager.Wasm.SipWrapper;

public class RTCPeerConnection
{
    internal static readonly ConcurrentDictionary<string, RTCPeerConnection> Registry = new();

    private readonly string _id;

    public event Action<RTCPeerConnectionState>? onconnectionstatechange;
    public event Action<RTCDataChannel>? ondatachannel;
    // null candidate signals ICE gathering complete, same as the browser's own event.
    public event Action<RTCIceCandidate?>? onicecandidate;

    /// <summary>
    /// Synchronous, matching the real browser API (only negotiation steps are
    /// Promise-based) — callers must have already awaited
    /// <see cref="WebRtcBridge.EnsureInitializedAsync"/> once at startup.
    /// </summary>
    public RTCPeerConnection(RTCConfiguration config)
    {
        if (!WebRtcBridge.IsInitialized)
            throw new InvalidOperationException(
                "WebRtcBridge.EnsureInitializedAsync() must be awaited once at startup before constructing any RTCPeerConnection.");

        var iceServersJson = JsonSerializer.Serialize(
            (config.iceServers ?? new List<RTCIceServer>()).Select(s => new { urls = s.urls }));
        _id = WebRtcBridge.CreatePeerConnection(iceServersJson);
        Registry[_id] = this;
    }

    public async Task<SetDescriptionResultEnum> setRemoteDescription(RTCSessionDescriptionInit description)
    {
        var result = await WebRtcBridge.SetRemoteDescription(_id, description.type.ToString(), description.sdp ?? "");
        return result == "OK" ? SetDescriptionResultEnum.OK : SetDescriptionResultEnum.Error;
    }

    public async Task<RTCSessionDescriptionInit> createAnswer(object? options = null)
    {
        var sdp = await WebRtcBridge.CreateAnswer(_id);
        return new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp };
    }

    public Task setLocalDescription(RTCSessionDescriptionInit description)
        => WebRtcBridge.SetLocalDescription(_id, description.type.ToString(), description.sdp ?? "");

    public Task addIceCandidate(RTCIceCandidateInit candidate)
        => WebRtcBridge.AddIceCandidate(_id, candidate.candidate ?? "", candidate.sdpMid, candidate.sdpMLineIndex);

    public void close()
    {
        WebRtcBridge.ClosePeerConnection(_id);
        Registry.TryRemove(_id, out _);
    }

    internal void RaiseIceCandidate(string? candidate, string? sdpMid, int sdpMLineIndex)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            onicecandidate?.Invoke(null);
            return;
        }

        onicecandidate?.Invoke(new RTCIceCandidate
        {
            candidate = candidate,
            sdpMid = sdpMid,
            sdpMLineIndex = (ushort)sdpMLineIndex,
        });
    }

    internal void RaiseConnectionStateChange(RTCPeerConnectionState state)
        => onconnectionstatechange?.Invoke(state);

    internal void RaiseDataChannel(RTCDataChannel channel)
        => ondatachannel?.Invoke(channel);
}

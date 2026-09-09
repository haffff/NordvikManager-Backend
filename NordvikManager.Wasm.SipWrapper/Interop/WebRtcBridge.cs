using System.Runtime.InteropServices.JavaScript;

namespace NordvikManager.Wasm.SipWrapper.Interop;

/// <summary>
/// The only place in this project that talks to JS. Everything else (RTCPeerConnection,
/// RTCDataChannel) is a plain C# object that looks up its counterpart here by a string id.
/// Peer-connection construction is synchronous (matches the real browser API — only
/// negotiation steps are Promise-based), so callers must await <see cref="EnsureInitializedAsync"/>
/// once at startup before constructing any RTCPeerConnection.
/// </summary>
internal static partial class WebRtcBridge
{
    private static Task? _initTask;

    /// <summary>True once <see cref="EnsureInitializedAsync"/> has completed — checked by
    /// RTCPeerConnection's constructor so a missed-await bug fails loudly with a clear
    /// message instead of an opaque JS interop error deep in CreatePeerConnection.</summary>
    internal static bool IsInitialized { get; private set; }

    internal static Task EnsureInitializedAsync()
        => _initTask ??= InitializeAsync();

    private static async Task InitializeAsync()
    {
        await JSHost.ImportAsync("nordvik-webrtc", "/webrtcInterop.js");
        IsInitialized = true;
    }

    // ---- Peer connection lifecycle ----

    [JSImport("createPeerConnection", "nordvik-webrtc")]
    internal static partial string CreatePeerConnection(string iceServersJson);

    [JSImport("closePeerConnection", "nordvik-webrtc")]
    internal static partial void ClosePeerConnection(string pcId);

    [JSImport("setRemoteDescription", "nordvik-webrtc")]
    internal static partial Task<string> SetRemoteDescription(string pcId, string type, string sdp);

    [JSImport("createAnswer", "nordvik-webrtc")]
    internal static partial Task<string> CreateAnswer(string pcId);

    [JSImport("setLocalDescription", "nordvik-webrtc")]
    internal static partial Task SetLocalDescription(string pcId, string type, string sdp);

    [JSImport("addIceCandidate", "nordvik-webrtc")]
    internal static partial Task AddIceCandidate(string pcId, string candidate, string? sdpMid, int sdpMLineIndex);

    // ---- Data channel ----

    [JSImport("dataChannelSend", "nordvik-webrtc")]
    internal static partial void DataChannelSend(string channelId, string text);

    // ---- JS -> C# dispatch (routed by id through the RTCPeerConnection/RTCDataChannel registries) ----

    [JSExport]
    internal static void OnIceCandidate(string pcId, string? candidate, string? sdpMid, int sdpMLineIndex)
    {
        if (RTCPeerConnection.Registry.TryGetValue(pcId, out var pc))
            pc.RaiseIceCandidate(candidate, sdpMid, sdpMLineIndex);
    }

    [JSExport]
    internal static void OnConnectionStateChange(string pcId, string state)
    {
        if (RTCPeerConnection.Registry.TryGetValue(pcId, out var pc) &&
            Enum.TryParse<RTCPeerConnectionState>(state, out var parsed))
            pc.RaiseConnectionStateChange(parsed);
    }

    [JSExport]
    internal static void OnDataChannel(string pcId, string channelId)
    {
        if (!RTCPeerConnection.Registry.TryGetValue(pcId, out var pc)) return;
        var channel = new RTCDataChannel(channelId);
        RTCDataChannel.Registry[channelId] = channel;
        pc.RaiseDataChannel(channel);
    }

    [JSExport]
    internal static void OnDataChannelOpen(string channelId)
    {
        if (RTCDataChannel.Registry.TryGetValue(channelId, out var channel))
            channel.RaiseOpen();
    }

    [JSExport]
    internal static void OnDataChannelClose(string channelId)
    {
        if (RTCDataChannel.Registry.TryRemove(channelId, out var channel))
            channel.RaiseClose();
    }

    [JSExport]
    internal static void OnDataChannelMessage(string channelId, byte[] data)
    {
        if (RTCDataChannel.Registry.TryGetValue(channelId, out var channel))
            channel.RaiseMessage(data);
    }
}

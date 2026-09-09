using NordvikManager.Wasm.SipWrapper.Interop;

namespace NordvikManager.Wasm.SipWrapper;

/// <summary>
/// Public entry point for hosts consuming this wrapper. Call
/// <see cref="InitializeAsync"/> once at startup, before constructing any
/// <see cref="RTCPeerConnection"/> — everything else in this namespace mirrors
/// SIPSorcery's API shape directly (RTCPeerConnection, RTCDataChannel, ...).
/// </summary>
public static class WebRtc
{
    public static Task InitializeAsync() => WebRtcBridge.EnsureInitializedAsync();
}

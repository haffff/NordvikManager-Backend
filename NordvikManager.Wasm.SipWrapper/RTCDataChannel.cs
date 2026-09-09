using System.Collections.Concurrent;
using NordvikManager.Wasm.SipWrapper.Interop;

namespace NordvikManager.Wasm.SipWrapper;

public class RTCDataChannel
{
    internal static readonly ConcurrentDictionary<string, RTCDataChannel> Registry = new();

    private readonly string _id;

    public RTCDataChannelState readyState { get; private set; } = RTCDataChannelState.connecting;

    // SIPSorcery fires onopen as a plain Action exactly once — the browser-native
    // channel behaves the same way, so callers that guard against a double-fire
    // (WebRTCSessionService.cs's Interlocked check) don't need to change.
    public event Action? onopen;
    public event Action? onclose;
    public event Action<RTCDataChannel, DataChannelPayloadProtocols, byte[]>? onmessage;

    internal RTCDataChannel(string id)
    {
        _id = id;
    }

    public void send(string text) => WebRtcBridge.DataChannelSend(_id, text);

    internal void RaiseOpen()
    {
        readyState = RTCDataChannelState.open;
        onopen?.Invoke();
    }

    internal void RaiseClose()
    {
        readyState = RTCDataChannelState.closed;
        onclose?.Invoke();
    }

    internal void RaiseMessage(byte[] data)
        => onmessage?.Invoke(this, DataChannelPayloadProtocols.WebRtcString, data);
}

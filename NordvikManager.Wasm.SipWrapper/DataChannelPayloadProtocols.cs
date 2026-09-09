namespace NordvikManager.Wasm.SipWrapper;

// Third parameter of RTCDataChannel.onmessage in SIPSorcery's real signature is a
// protocol discriminator (text vs binary frame). WebRTCSessionService.cs discards it
// (`(_, _, data) => ...`) — kept only so that lambda's arity still matches.
public enum DataChannelPayloadProtocols
{
    WebRtcString,
    WebRtcBinary,
}

namespace NordvikManager.Wasm.SipWrapper;

// SIPSorcery-specific result shape (the browser's setRemoteDescription just
// resolves/rejects a Promise) — kept only because WebRTCSessionService.cs checks
// `descResult != SetDescriptionResultEnum.OK`. Only OK vs "anything else" is
// actually observed by that call site; Error is what a rejected JS promise maps to.
public enum SetDescriptionResultEnum
{
    OK,
    Error,
}

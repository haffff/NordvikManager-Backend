using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NordvikManager.Wasm.WebRtc
{
    /// <summary>
    /// <see cref="IBrowserRtc"/> over the browser's RTCPeerConnection: Interop/nordvik-webrtc.js,
    /// through [JSImport]. Its [JSExport]s are the JS module's callbacks, handed to
    /// <see cref="BrowserRtcRouter.Default"/>. Call <see cref="InitializeAsync"/> once before use.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public sealed partial class JsBrowserRtc : IBrowserRtc
    {
        private const string Module = "nordvik-webrtc";

        /// <summary>Loads the JS module and gives it this assembly's callbacks.</summary>
        /// <param name="moduleUrl">
        /// Where the host serves nordvik-webrtc.js. Relative URLs resolve against the runtime's
        /// _framework/ folder, not the page: the default is the module next to the page.
        /// </param>
        public static async Task InitializeAsync(string moduleUrl = "../nordvik-webrtc.js")
        {
            await JSHost.ImportAsync(Module, moduleUrl);
            await Init(typeof(JsBrowserRtc).Assembly.GetName().Name!);
        }

        public void CreatePeer(string peerId, string iceServersJson) => JsCreatePeer(peerId, iceServersJson);

        public Task<string?> SetRemoteOfferAsync(string peerId, string sdp) => JsSetRemoteOffer(peerId, sdp);

        public Task<string> CreateAnswerAsync(string peerId) => JsCreateAnswer(peerId);

        public void AddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex)
            => JsAddIceCandidate(peerId, candidate, sdpMid, sdpMLineIndex);

        public void ClosePeer(string peerId) => JsClosePeer(peerId);

        public void Send(string channelId, string message) => JsSend(channelId, message);

        public double BufferedAmount(string channelId) => JsBufferedAmount(channelId);

        [JSImport("init", Module)]
        private static partial Task Init(string assemblyName);

        [JSImport("createPeer", Module)]
        private static partial void JsCreatePeer(string peerId, string iceServersJson);

        [JSImport("setRemoteOffer", Module)]
        private static partial Task<string?> JsSetRemoteOffer(string peerId, string sdp);

        [JSImport("createAnswer", Module)]
        private static partial Task<string> JsCreateAnswer(string peerId);

        [JSImport("addIceCandidate", Module)]
        private static partial void JsAddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex);

        [JSImport("closePeer", Module)]
        private static partial void JsClosePeer(string peerId);

        [JSImport("send", Module)]
        private static partial void JsSend(string channelId, string message);

        [JSImport("bufferedAmount", Module)]
        private static partial double JsBufferedAmount(string channelId);

        // ---- callbacks from nordvik-webrtc.js ----

        [JSExport]
        internal static void OnStateChanged(string peerId, string state)
            => BrowserRtcRouter.Default.OnStateChanged(peerId, state);

        [JSExport]
        internal static void OnIceCandidate(string peerId, string? candidate, string? sdpMid, int sdpMLineIndex)
            => BrowserRtcRouter.Default.OnIceCandidate(peerId, candidate, sdpMid, sdpMLineIndex);

        [JSExport]
        internal static void OnDataChannel(string peerId, string channelId, bool isOpen)
            => BrowserRtcRouter.Default.OnDataChannel(peerId, channelId, isOpen);

        [JSExport]
        internal static void OnChannelOpen(string channelId)
            => BrowserRtcRouter.Default.OnChannelOpen(channelId);

        [JSExport]
        internal static void OnChannelClose(string channelId)
            => BrowserRtcRouter.Default.OnChannelClose(channelId);

        [JSExport]
        internal static void OnChannelMessage(string channelId, string text)
            => BrowserRtcRouter.Default.OnChannelMessage(channelId, text);
    }
}

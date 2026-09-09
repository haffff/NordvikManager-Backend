using NordvikManager.Wasm.SipWrapper;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Validates NordvikManager.Wasm.SipWrapper by replicating the exact call sequence
    /// and event-wiring order that DNDOnePlaceManager/WebRTC/WebRTCSessionService.cs
    /// uses (OnPeerJoined / OnOfferReceived / OnIceCandidateReceived), with the real
    /// signaling/lobby/dispatcher collaborators replaced by stubs.
    ///
    /// What this proves: the wrapper is a faithful drop-in for the SIPSorcery surface
    /// WebRTCSessionService.cs relies on (sync ctor, buffered-then-flushed ICE
    /// candidates, the onopen-fires-once-plus-late-subscribe race guard, unstripped
    /// browser-formatted ICE candidate strings, chunked sends via the verbatim-copied
    /// WebRTCPlayerConnection).
    ///
    /// What this does NOT prove: that WebRTCSessionService.cs itself runs under wasm —
    /// that file also depends on ILobbyRegistry/IWebRTCApiDispatcher/GameLobby/etc.,
    /// which live in DNDOnePlaceManager (Microsoft.NET.Sdk.Web) and cannot be
    /// referenced from a browser-wasm project at all. Extracting those into a shared,
    /// non-Web-SDK project is unstarted work, tracked separately in the plan.
    /// </summary>
    public static partial class SessionServiceHarness
    {
        private static readonly TaskCompletionSource _harnessComplete =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Mirrors WebRTCSessionService's _pendingLocalCandidates for the single peer
        // this harness drives — null once the answer has been sent.
        private static List<(string Candidate, string? SdpMid, int SdpMLineIndex)>? _pendingLocalCandidates = new();

        private static WebRTCPlayerConnection? _connection;
        private static string? _expectedPayload;
        // Captured directly rather than looked up via RTCPeerConnection's registry —
        // that registry is internal to the SipWrapper assembly and not visible here;
        // this harness only ever drives one connection anyway.
        private static RTCPeerConnection? _pc;

        [JSImport("startPeerA", "test-signaling")]
        internal static partial Task<string> StartPeerA();

        [JSImport("submitAnswer", "test-signaling")]
        internal static partial Task SubmitAnswer(string sdp);

        [JSImport("submitCandidateFromPeerB", "test-signaling")]
        internal static partial Task SubmitCandidateFromPeerB(string candidate, string? sdpMid, int sdpMLineIndex);

        public static async Task RunAsync()
        {
            Console.WriteLine("[Harness] Initializing WebRtc wrapper...");
            await WebRtc.InitializeAsync();
            await JSHost.ImportAsync("test-signaling", "/testSignaling.js");

            // ---- OnPeerJoined equivalent (WebRTCSessionService.cs:117-300) ----
            var pc = new RTCPeerConnection(new RTCConfiguration { iceServers = new() });
            _pc = pc;

            pc.onconnectionstatechange += state =>
                Console.WriteLine($"[Harness] connection state -> {state}");

            pc.ondatachannel += dataChannel =>
            {
                var openHandled = 0;
                void HandleOpen()
                {
                    if (Interlocked.Exchange(ref openHandled, 1) != 0) return;
                    _connection = new WebRTCPlayerConnection(dataChannel);
                    Console.WriteLine("[Harness] data channel open — WebRTCPlayerConnection constructed.");
                    _ = SendChunkedTestPayloadAsync();
                }

                dataChannel.onopen += HandleOpen;
                // Same late-subscribe race guard as WebRTCSessionService.cs:236-240.
                if (dataChannel.readyState == RTCDataChannelState.open)
                    HandleOpen();

                dataChannel.onmessage += (_, _, _) => { }; // GM does not receive on this channel in this test
            };

            pc.onicecandidate += candidate =>
            {
                if (candidate == null) return;
                if (_pendingLocalCandidates is { } pending)
                {
                    lock (pending)
                        pending.Add((candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex));
                }
                else
                {
                    _ = SubmitCandidateFromPeerB(candidate.candidate, candidate.sdpMid, candidate.sdpMLineIndex);
                }
            };

            // ---- Drive peerA (the "player") to produce an offer ----
            var offerSdp = await StartPeerA();

            // ---- OnOfferReceived equivalent (WebRTCSessionService.cs:302-355) ----
            var descResult = await pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offerSdp,
            });

            if (descResult != SetDescriptionResultEnum.OK)
                throw new InvalidOperationException($"setRemoteDescription failed: {descResult}");

            var answer = await pc.createAnswer(null);
            await pc.setLocalDescription(answer);

            await SubmitAnswer(answer.sdp!);
            Console.WriteLine("[Harness] Answer sent to peerA.");

            // Flush candidates buffered during negotiation (WebRTCSessionService.cs:429-445).
            List<(string, string?, int)> snapshot;
            lock (_pendingLocalCandidates!)
                snapshot = new(_pendingLocalCandidates);
            _pendingLocalCandidates = null;
            foreach (var (candidate, sdpMid, sdpMLineIndex) in snapshot)
                await SubmitCandidateFromPeerB(candidate, sdpMid, sdpMLineIndex);

            var completed = await Task.WhenAny(_harnessComplete.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            if (completed != _harnessComplete.Task)
                HarnessReport.Record("SessionServiceHarness", false, "did not complete within 15s");
        }

        // ---- OnIceCandidateReceived equivalent (WebRTCSessionService.cs:357-397) ----
        // No "candidate:" prefix stripped — the browser-formatted string goes straight
        // to addIceCandidate, unlike the SIPSorcery-specific stripping this replaces.
        [JSExport]
        internal static async void OnCandidateFromPeerA(string candidate, string? sdpMid, int sdpMLineIndex)
        {
            try
            {
                if (_pc == null) return;

                await _pc.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = (ushort)sdpMLineIndex,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Harness] addIceCandidate threw: {ex.Message}");
            }
        }

        [JSExport]
        internal static void OnMessageFromPeerB(string text)
        {
            var ok = _expectedPayload != null && text == _expectedPayload;
            var message = ok
                ? "chunked payload reassembled correctly by peerA over a real RTCDataChannel"
                : $"reassembled payload did not match (got {text.Length} chars)";
            Console.WriteLine(ok
                ? $"[Harness] RESULT: PASS — {message}."
                : $"[Harness] RESULT: FAIL — {message}.");
            HarnessReport.Record("SessionServiceHarness", ok, message);
            _harnessComplete.TrySetResult();
        }

        private static async Task SendChunkedTestPayloadAsync()
        {
            if (_connection == null) return;
            // >15KB so WebRTCPlayerConnection.SendMessageToPlayer's chunking path runs.
            var big = new string('x', 20_000);
            _expectedPayload = "\"" + big + "\""; // JsonConvert.SerializeObject(string) wraps it in quotes
            await _connection.SendMessageToPlayer(big);
        }
    }
}

# NordvikManager.Wasm.WebRtc browser check

Checks `NordvikManager.Wasm.WebRtc` against a real browser's WebRTC, the part unit tests (`BrowserPeerConnectionTests`, fake `IBrowserRtc`) can't cover: `nordvik-webrtc.js`, the JS interop, browser-wasm runtime differences.

A plain JS `RTCPeerConnection` (`player.js`, offering like the player client) connects over loopback to a GM peer made by `BrowserPeerConnectionFactory` + `JsBrowserRtc`. It checks offer/answer (and that a bad offer gives the browser's error), local candidates, remote candidates that all arrive before the offer is set (the offer SDP carries none and the GM's candidates are not sent to the player, so connecting needs them flushed from the queue; a refused candidate fails the run too), the data channel opening, messages both ways (non-ASCII included), TURN credentials being accepted, and closing.

Needs the `wasm-tools` workload, Node, Playwright and Chrome. Not in the solution, so CI and `dotnet test` don't need the workload.

```bash
dotnet build NordvikManager.Wasm.WebRtc.BrowserCheck -c Release --artifacts-path <out>
node NordvikManager.Wasm.WebRtc.BrowserCheck/run.cjs <out>/bin/NordvikManager.Wasm.WebRtc.BrowserCheck/release_browser-wasm/AppBundle [path/to/playwright]
# CHROME_PATH=... to use another Chrome/Chromium; exit code 0 = PASS
```

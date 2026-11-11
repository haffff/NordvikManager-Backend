# NordvikManager.Wasm.Host

The browser-wasm GM host: the GM backend running in a browser tab instead of the native server. It starts `WasmHost` (`NordvikManager.Wasm.Hosting`), which builds the same layers as the native server (`InfrastructureLayerModule`, `ApplicationLayerModule`, `RealTimeLayerModule`) with the browser's own WebRTC (`NordvikManager.Wasm.WebRtc`), WebSocket-only signaling and an in-memory database.

Not in the solution: browser-wasm needs the `wasm-tools` workload. The composition is in the solution and tested on the desktop (`WasmHostTests`).

## What works so far

Only the self-check (`index.html?selfcheck`). It runs the host in the tab as it would host a game, with Central left out: the real `WebRTCSessionService` and `GameLobby` serve an in-page player (`SelfCheck/selfcheck-player.js`, speaking the player client's data channel protocol) connected through in-page signaling. Each step prints a `CHECK` line:

- the host builds (every registration validated);
- MediatR + EF Core (in memory) create and list a game;
- the lobby opens; the player connects over the browser's WebRTC;
- the heartbeat is answered;
- tunnelled REST through `ApiRouter` and the shared endpoints: `GetPlayer`, `GetFullGame`, `session/whoami` (the channel's game), `user/userinfo` (the caller); a server-only route (`api/meta`) answers 501 saying so;
- a file uploaded in parts (`materials/UploadStart`, `UploadPart`, `UploadFinish`: upload store, fixed quota, database) and read back through `materials/resource`; addon step definitions;
- a chat message goes through `GameLobby` (player resolution, `ChatHandler`, MediatR) and comes back;
- a 31 504-char chat (emoji included) goes both ways chunked: chunk reassembly and the send pump.

## Not yet

- Server-only routes answer 501 by design (backup, server settings, updates, Central's meta, linking files on a disk); everything else a player client calls is answered.
- Hosting a real game: login, Central session, signaling to Central (Central's CORS must allow the host's origin), where the host is served from.
- Keeping games across a reload (browser storage).

## Run the self-check

Needs the `wasm-tools` workload, Node, Playwright and Chrome.

```bash
dotnet build NordvikManager.Wasm.Host -c Release --artifacts-path <out>
node NordvikManager.Wasm.Host/selfcheck.cjs <out>/bin/NordvikManager.Wasm.Host/release_browser-wasm/AppBundle [path/to/playwright]
# CHROME_PATH=... for another Chrome/Chromium; exit code 0 = PASS
```

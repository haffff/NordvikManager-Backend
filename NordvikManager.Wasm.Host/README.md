# NordvikManager.Wasm.Host

The browser-wasm GM host: the GM backend running in a browser tab instead of the native server. It starts `WasmHost` (`NordvikManager.Wasm.Hosting`), which builds the same layers as the native server (`InfrastructureLayerModule`, `ApplicationLayerModule`, `RealTimeLayerModule`) with the browser's own WebRTC (`NordvikManager.Wasm.WebRtc`), WebSocket-only signaling and an in-memory database.

Not in the solution: browser-wasm needs the `wasm-tools` workload. The composition is in the solution and tested on the desktop (`WasmHostTests`).

## The GM's page

`index.html` is the GM's page: log in to Central, see your games, create one, start or stop its session. The host runs in the tab (`HostPage`, `HostGm`): the GM's Central tokens are held by the host (a browser can't keep Central's cookies), and the GM's actions run the same shared endpoints as everyone, with those tokens. The logged-in GM is the host's local admin. Central is the page's own origin (serve the host from Central's domain and no CORS is needed), or `?central=https://...`; otherwise Central's `CORS_ORIGIN` must list the page's origin. Keep the tab open while playing. Games live in memory: a reload starts empty (and creates new Central sessions).

`gm-e2e.cjs` drives it against a real Central in headless Chrome (log in, create a game, start, stop):

```bash
# a Central of your own: CORS_ORIGIN=http://localhost:<port>, an account (npm run seed with ADMIN_USERNAME/ADMIN_PASSWORD)
node NordvikManager.Wasm.Host/gm-e2e.cjs <AppBundle> [path/to/playwright] <port> <centralUrl> <user> <password>
```

`player-e2e.cjs` is the whole thing with a real player client: the GM hosts a game from the GM page, then the player client (a `REACT_APP_MODE=player` build of the frontend, with `REACT_APP_CENTRAL_URL` set to the test Central) opens the invite link `?game=<Central session>`, logs in, joins, sends a chat message, reloads and sees it again. It makes the player's account itself (an invite from the GM, then register) and prints what the host didn't answer 2xx.

```bash
# Central: CORS_ORIGIN=http://localhost:<hostPort>,http://localhost:<playerPort>; gmtest / gm-test-pass-1 seeded
node NordvikManager.Wasm.Host/player-e2e.cjs <host AppBundle> <player build> <playwright> <centralUrl> <hostPort> <playerPort>
```

## The self-check

`index.html?selfcheck`. It runs the host in the tab as it would host a game, with Central left out: the real `WebRTCSessionService` and `GameLobby` serve an in-page player (`SelfCheck/selfcheck-player.js`, speaking the player client's data channel protocol) connected through in-page signaling. Each step prints a `CHECK` line:

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
- Built-in addons (Basics): the native server zips them from its BuiltInAddons folder, which a browser host doesn't have, so new games get none.
- Where the host page is served from (from Central's domain, it needs no CORS).
- Keeping games across a reload (browser storage).

## Run the self-check

Needs the `wasm-tools` workload, Node, Playwright and Chrome.

```bash
dotnet build NordvikManager.Wasm.Host -c Release --artifacts-path <out>
node NordvikManager.Wasm.Host/selfcheck.cjs <out>/bin/NordvikManager.Wasm.Host/release_browser-wasm/AppBundle [path/to/playwright]
# CHROME_PATH=... for another Chrome/Chromium; exit code 0 = PASS
```

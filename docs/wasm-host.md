# Browser-WASM GM host — architecture & extraction notes

Status: **proof of concept.** This documents the design of running the NordvikManager
GM backend as a `browser-wasm` .NET module, and the constraints that shaped it. The
phased roadmap (what is proven, what remains) lives outside the repo in the planning
notes; this file is the "how it fits together" reference.

## Why

Today a game master (GM) must run the native `DNDOnePlaceManager` ASP.NET Core process
to host a session. The goal of this POC is a **zero-install alternative**: the GM opens
a web page, a `browser-wasm` build of the same backend runs in the tab, and players
connect to it P2P over WebRTC data channels exactly as they would to the native host.
The native server stays the primary/advanced path — the browser host runs *alongside*
it, sharing code, tolerating some divergence.

## The three-process topology (unchanged by this POC)

```
   Player browser (React)                     GM  ── native DNDOnePlaceManager  (primary)
        │                                         └─ OR browser-wasm NordvikManager.Wasm.Host  (this POC)
        │  WebRTC data channel (game state + tunneled REST)
        ▼
   ┌─────────────────────────── Central Server (Node) ───────────────────────────┐
   │  auth  ·  game-session registry  ·  WebRTC signaling relay (Socket.IO)        │
   └─────────────────────────────────────────────────────────────────────────────┘
```

Central never sees SDP contents — it is a pure relay. The GM host owns all game state
and the `GameLobby` real-time logic; the browser build changes *where that runs*, not
the protocol players speak.

## Projects

| Project | SDK / RID | Role |
|---|---|---|
| `DndOnePlaceManager.RealTime` | `Microsoft.NET.Sdk`, plain `net10.0` | The real-time/lobby layer, **extracted from `DNDOnePlaceManager`** so a `browser-wasm` project can reference it. `GameLobby`, `ActionProcessingService`, the 12 `IWebSocketHandler`s, the action-step engine, and the WebRTC/signaling **interfaces** (`ISignalingService`, `IWebRTCSessionService`, `IPlayerConnection`, `IWebRTCApiDispatcher`, `ILobbyRegistry`) plus `SignalingService` and `CentralServerService`. |
| `NordvikManager.Wasm.SipWrapper` | `browser-wasm` classlib | A **SIPSorcery-API-shaped shim** over the browser's native `RTCPeerConnection`/`RTCDataChannel`, via `System.Runtime.InteropServices.JavaScript`. Same type/member names the ported `WebRTCSessionService` calls. |
| `NordvikManager.Wasm.Host` | `browser-wasm` exe (`WasmMainJSPath=main.js`) | The composition root. Ports `WebRTCSessionService` + `WebRTCPlayerConnection` against the shim, re-implements the REST tunnel (`Rest/`), and carries the harness suite. |

`DNDOnePlaceManager` (the native API, `Microsoft.NET.Sdk.Web`) references
`DndOnePlaceManager.RealTime` and is otherwise unchanged except for two reflection
scans retargeted from `Assembly.GetExecutingAssembly()` to
`typeof(IWebSocketHandler).Assembly` / `typeof(IActionStepDefinition).Assembly`
(the handlers moved assemblies).

### The Web-SDK wall

`Microsoft.NET.Sdk.Web` pulls in the `Microsoft.AspNetCore.App` framework reference
for the whole project, and that framework has **no `browser-wasm` runtime pack**. A
`browser-wasm` project therefore cannot reference `DNDOnePlaceManager` at all. The
extraction rule for `DndOnePlaceManager.RealTime` is: **every file moved in had zero
`Microsoft.AspNetCore.*` references**. Files that don't meet that bar
(`WebRTCInProcessDispatcher` — replays HTTP through the ASP.NET Core pipeline;
`SessionController`; the auth handlers) stay in `DNDOnePlaceManager` and are
re-implemented or mirrored on the wasm side.

### The extraction is a pure move

`DndOnePlaceManager.RealTime`'s 111 files are `git mv` with **no content change**
(`git diff -M` reports 0 insertions / 0 deletions). Exactly three carry follow-up
wasm-compat edits — see "wasm-specific deltas" below.

## `NordvikManager.Wasm.Host` composition root (`Program.cs`)

- Builds one `ServiceCollection`: `SimpleWasmLoggerProvider`, `AddHttpClient`,
  `ApplicationLayerModule.Register`, **`AddDbContext<IDbContext, DndOneContext>(o => o.UseInMemoryDatabase(...))`**
  (by hand — `InfrastructureLayerModule.Register` is **not** called, it wires
  SQLite/MySQL unconditionally), then the RealTime services (`LobbyRegistry`,
  `ActionProcessingService`, `LobbyGameEventLogger`), `WasmFileStorageProvider`,
  `CentralServerService`, `WebRTCRestDispatcher`, `WebRTCSessionService`.
- `ISignalingService` is the fork point: `TestSignalingService` (harness) vs. the real
  `SignalingService` (GM mode).
- Reflection-scans `IWebRTCRestHandler`, `IWebSocketHandler`, `IActionStepDefinition`
  out of their referenced assemblies.

### Two entry modes

| | `index.html` — harness | `gm.html` — real GM |
|---|---|---|
| Selected by | `window.__nordvikMode` unset → `"harness"` | inline `window.__nordvikMode='gm'` before `main.js` |
| Detected in C# | `HostMode.IsRealSessionAsync()` via `hostMode.js` | same |
| Signaling | `TestSignalingService` — fake transport, in-page JS `peerA` (`testSignaling.js`) | real `SignalingService` (Socket.IO) against a real Central Server |
| Runs | `GameLobbyHarness` → `CentralSignalingSpike` → `WebRTCSessionServiceHarness` → `SessionServiceHarness`, then aggregate report + `window.close()` | `RealGmSession.RunAsync` — login, create game + Central session, `StartSessionAsync`, then `Task.Delay(Timeout.Infinite)` staying alive as the GM backend |

`RealGmSession` currently hardcodes `poc` / `pocpocpoc` and `CentralServerUrl`
`http://localhost:3000`.

## wasm-specific deltas (the gotchas)

Everything a `browser-wasm` .NET runtime does differently that this POC had to work around:

- **No ASP.NET Core hosting.** No Kestrel, no middleware pipeline, no controllers. The
  data-channel REST tunnel is re-implemented as `Rest/WebRTCRestDispatcher` +
  `IWebRTCRestHandler` (`{method,path,body} → mediator.Send`), replacing
  `WebRTCInProcessDispatcher`.
- **`Set-Cookie` is never visible to script** (Fetch API security). `CentralServerService`
  reads `accessToken` from the JSON login *body* and sends `Authorization: Bearer …`
  instead of a `Cookie` header (which `fetch` also forbids setting).
- **Socket.IO polling handshake fails.** engine.io's first GET carries a body, which
  `fetch` rejects. `SignalingService` gained `Signaling:ForceWebSocketTransport` —
  WebSocket-only transport, and it drops the handshake `Cookie` header (a wasm
  `ClientWebSocket` can't set request headers). The wasm host sets this flag; the
  native host leaves it off.
- **`AddConsole()` throws `PlatformNotSupportedException`** (terminal detection).
  `SimpleWasmLoggerProvider` writes to `Console.Out`, which the runtime routes to the
  browser console.
- **SQLite/MySQL EF providers can't run and bloat the bundle.** The relational provider
  packages (`Microsoft.EntityFrameworkCore.Sqlite`, `MySql.EntityFrameworkCore`) live in
  the `DNDOnePlaceManager` Web project only — not in `DndOnePlaceManager.Infrastructure`,
  which the wasm graph reaches transitively. `IDbContext` + its provider is registered in
  `Startup.ConfigureServices`, not in `InfrastructureLayerModule.Register`. Moving them out
  cut the `_framework` payload from **42.6 MB to 32.5 MB** (BouncyCastle, MySql.Data,
  MySqlConnector, Google.Protobuf, the SQLitePCLRaw native stack). `Infrastructure` keeps
  `Microsoft.EntityFrameworkCore.Relational` (provider-agnostic — `GetDbConnection` /
  `ExecuteSqlRaw` / `HasFilter` in `DndOneContext`). The wasm host and the unit tests use
  EF Core InMemory. Persistence across a tab reload is future work.
- **No local filesystem.** `WasmFileStorageProvider` throws on every call; ported REST
  routes force `ResourceStorageKind.Blob` (bytes in the DB). Filesystem-linking routes
  (`LinkResource`, `LinkDirectory`, `BrowseLocalDirectory`, `TransferResourceStorage`)
  are not ported.
- **Single-threaded.** No OS threads, no `BackgroundService`, no `Timer`. `Task.Run`
  still schedules cooperatively but with no parallelism; a CPU-bound action hook blocks
  the UI thread. `GameLobby.ConnectedPlayers` (a plain `Dictionary`) is unlocked —
  fine for the current one-peer-at-a-time reality, unreviewed for many.
- **Two assemblies expose `[JSExport]`.** `main.js` calls `getAssemblyExports` for both
  the main assembly and `NordvikManager.Wasm.SipWrapper`.

## The shim (`NordvikManager.Wasm.SipWrapper`)

`Interop/WebRtcBridge.cs` is the only file that talks to JS. Everything else
(`RTCPeerConnection`, `RTCDataChannel`) is a plain C# object that finds its JS
counterpart by string id through a `ConcurrentDictionary` registry
(`[JSImport]`/`[JSExport]` can't marshal opaque JS handles). `Interop/webrtcInterop.js`
is the JS half; consumers deploy it via `WasmExtraFilesToDeploy`.

**Covered:** exactly the SIPSorcery surface the ported `WebRTCSessionService` /
`WebRTCPlayerConnection` touch — synchronous ctor, `onconnectionstatechange` /
`ondatachannel` / `onicecandidate`, `setRemoteDescription` (→ `SetDescriptionResultEnum`)
/ `createAnswer` / `setLocalDescription` / `addIceCandidate` / `close`, and
`RTCDataChannel` `readyState` / `onopen` / `onclose` / `onmessage` / `send(string)`.

**Not covered** (unused by the current port): offerer role (`createOffer`,
`createDataChannel`), **TURN credentials** (`RTCIceServer` carries only `urls` — no
`username`/`credential`, so an authenticated TURN server from Central `/api/meta` is
silently reduced to STUN), binary `send(byte[])`, `getStats`, and `close()` does not
purge the associated `RTCDataChannel.Registry` entries.

## Harness map (`index.html` mode)

| Harness | Proves | Real vs. faked |
|---|---|---|
| `GameLobbyHarness` | `GameLobby → ChatHandler → MediatR → EF InMemory` round-trips under wasm | real lobby; fake `IPlayerConnection`; no WebRTC |
| `CentralSignalingSpike` | a wasm GM authenticates Socket.IO signaling against a **real** Central Server (WS-only transport) | real `SignalingService` + real Central Server on `:3000`; needs the `poc` account |
| `WebRTCSessionServiceHarness` | the **real** `WebRTCSessionService` + REST-tunnel dispatcher, end to end (`peer-joined → offer → ICE → data-channel → chat + REST read/write routes`) | real service & lobby; `TestSignalingService` fakes the transport; in-page `peerA` |
| `SessionServiceHarness` | the **shim** in isolation — replicates `WebRTCSessionService`'s call/event order, 20 KB payload chunked and reassembled by a real `RTCDataChannel` | shim + real native `RTCPeerConnection`; in-page `peerA` |

Every harness records its result through `Harness/HarnessReport.cs`; `Program.cs`
posts **one aggregate** to `serve.js` (`harness-result.json`) in a `finally`, so a
partial or throwing run still leaves a durable result. Every green result so far is
**in-page loopback** — no offer from a real remote browser has reached a wasm GM yet.

## Build & run

```bash
# harness suite
dotnet build NordvikManager.Wasm.Host -c Release
node NordvikManager.Wasm.Host/serve.js 8098      # serves bin/Release/net10.0/browser-wasm/AppBundle
#   open http://localhost:8098/         → runs the harness suite, writes harness-result.json

# real GM session (needs a Central Server on :3000 with a 'poc'/'pocpocpoc' account)
#   open http://localhost:8098/gm.html  → logs in, creates 'wasm-gm-real-session', stays alive
```

The published bundle lands in `bin/<config>/net10.0/browser-wasm/AppBundle/`.

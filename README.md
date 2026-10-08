# Nordvik Manager backend (Previously named DNDOnePlaceManager)

This is the "GM Local Server" part of the Nordvik Manager VTT application — it runs on the Game Master's own machine and owns the actual game state. For release please go to [main repository](https://github.com/haffff/NordvikManager).

## How to run
### Backend:
1. Change `JWTSecret` in `AppSettings.Development.json` (or leave unset — a secret is auto-generated at startup).
2. If you want to use MySQL instead of the SQLite default, set `UseSqlite` to `false` and fill in `ConnectionStrings:DBData`.
3. `dotnet run --project DNDOnePlaceManager` (.NET 10) — serves the API on `http://localhost:8213` / `https://localhost:8214` by default.

### Database
SQLite (`DNDOnePlaceManager/data.db`) is created and migrated automatically at startup with EF Core migrations. Until the first public release the schema is kept in a single `InitialCreate` migration that is regenerated when it changes — after pulling a schema change, delete `data.db` (and its `-wal`/`-shm` files) and start again. MySQL has no migrations yet: it creates its tables from the model on first start.

### Built-in addons
`DNDOnePlaceManager/BuiltInAddons/` holds addons that ship with the server and are offered when a game is created — today **Basics** (a rich-text Note template and a generic, card-less token with two bars and status icons).

To run this alongside Central and both Frontend roles in one command instead, use `pnpm run dev` from the [main repo](https://github.com/haffff/NordvikManager#development-setup).

### Frontend
Frontend repository can be found [here](https://github.com/haffff/NordvikManagerFrontEnd). Run it in development mode (`pnpm start`) or build it (`pnpm build`) and drop the output into this project's `wwwroot` directory.

### Central server
This backend does not accept connections from players directly — it connects **out** to the [Central server](https://github.com/haffff/NordvikManager-Central) (`CentralServerUrl` in config) to exchange WebRTC signaling for each active game session, then talks to players and the GM's own browser over a peer-to-peer WebRTC data channel. See the [main repository's architecture diagram](https://github.com/haffff/NordvikManager#architecture) for how the three components fit together.

## Architecture

Clean Architecture with CQRS (MediatR) — see `CLAUDE.md` in this repo for the full layering rules, the command/handler pattern, and permission model.

In-game traffic (REST calls tunneled over WebRTC, plus real-time broadcast commands like token moves or map switches) is dispatched from `DNDOnePlaceManager/WebRTC/`:
- `SignalingService` holds one Socket.IO client per active session, connected to the Central server, and relays offer/answer/ICE messages to establish the peer-to-peer `RTCPeerConnection`.
- `WebRTCInProcessDispatcher` replays tunneled REST requests through the real ASP.NET Core pipeline (so ordinary controllers/MediatR handlers serve them — no separate route mapping needed).
- Real-time broadcast commands are dispatched through `DNDOnePlaceManager/WebSockets/Handlers/` (`IWebSocketHandler` implementations, auto-discovered via reflection). Despite the name, these no longer run over a raw WebSocket — that endpoint has been removed; they're invoked from `GameLobby` for messages arriving over the same WebRTC data channel via `WebRTCPlayerConnection`.

Live playback state (which playlist is playing, its track and volume) lives in memory on the `GameLobby`; playlists, soundboards, their volumes and everything else are stored in the database.

Rest of documentation in progress

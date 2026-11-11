# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Test & Run

```bash
dotnet build                                                          # Build solution
dotnet run --project NordvikManager                               # Run API (http://localhost:8213)

dotnet test                                                           # Run all tests
dotnet test NordvikManager.Tests                                  # Integration/controller tests only
dotnet test NordvikManager.Application.UnitTests                  # Unit tests only
dotnet test --filter "Name~GetGames_ReturnsOk"                        # Single test by name
dotnet test --filter "ClassName=GameListControllerTests"              # Single test class
```

Release builds: `dotnet publish NordvikManager/NordvikManager.csproj -c Release -r win-x64 -o <dir>` (never the `.sln`, that drags the test projects in). With a RID the csproj makes one self-contained, untrimmed single-file exe; the folder holds only it, `appsettings.json`, `wwwroot/` and `BuiltInAddons/`, and `data/` appears on first start. Outside Development, `Program.Main` switches the current folder to the exe's folder (`Hosting/AppRoot.cs`), so every relative path in appsettings resolves next to the exe however it was started.

The console is the GM's UI (`Hosting/`): outside Development it shows only warnings plus a startup banner (`StartupBanner`: addresses, Central reachability, data/log folders), opens the browser (`BrowserLauncher`; off with `--no-browser` or `"OpenBrowser": false`), writes the full log to `data/logs/nordvik-<date>.log` (`FileLoggerProvider`), and a failed start prints a plain-language reason (`FatalErrorMessage`) and waits for Enter. Never print secrets to the console.

Frontend (separate repo) can be placed as a build in `wwwroot/` or run via `npm start` from the [frontend repo](https://github.com/haffff/NordvikManagerFrontEnd).

## Architecture

Clean Architecture with CQRS. Dependency flow is strictly one-directional:

```
NordvikManager (API/Presentation: ASP.NET Core host, controllers, SIPSorcery; picks the adapters below)
    → NordvikManager.Infrastructure.Imaging  (adapter: SkiaSharp thumbnails — native host only)
    → NordvikManager.Infrastructure.MySql  (adapter: MySQL provider — native host only)
    → NordvikManager.Api  (REST endpoints without ASP.NET Core: the controllers' logic, ApiRouter)
    → NordvikManager.RealTime  (lobby, WebSocket-command handlers, action engine, WebRTC session + signaling)
    → NordvikManager.Application  (commands, handlers, DTOs, services)
    → NordvikManager.Infrastructure  (EF Core DbContexts, repos, HTTP clients)
    → NordvikManager.Domain  (entities, interfaces, enums — no external deps)
```

`NordvikManager.RealTime` is a plain `Microsoft.NET.Sdk` library with no ASP.NET Core and no SIPSorcery, so a browser-wasm GM host can reference it too. Keep it that way: anything needing ASP.NET Core (`HttpContext`, `IFormFile`, middleware, controllers) or a native WebRTC stack goes in `NordvikManager`. It keeps the `NordvikManager.*` namespaces it had before it was split out. WebRTC is reached through `IPeerConnectionFactory` (`WebRTC/IPeerConnection.cs`); the native server registers `SipsorceryPeerConnectionFactory`, a browser-wasm host `BrowserPeerConnectionFactory` (`NordvikManager.Wasm.WebRtc`: the browser's own RTCPeerConnection through `JsBrowserRtc` + `Interop/nordvik-webrtc.js`; unit-tested with a fake `IBrowserRtc`, checked in a real browser by `NordvikManager.Wasm.WebRtc.BrowserCheck`, which is outside the solution). The browser-wasm GM host is `NordvikManager.Wasm.Hosting` (`WasmHost`: the composition, in the solution, `WasmHostTests`) started by `NordvikManager.Wasm.Host` (browser-wasm, outside the solution; for now only its `?selfcheck`, see its README). In browser-wasm, reflection-based `System.Text.Json` is off by default: the shared layers use it, so a wasm host sets `<JsonSerializerIsReflectionEnabledByDefault>true</JsonSerializerIsReflectionEnabledByDefault>` (the browser check does); code meant only for wasm uses source-generated contexts. `RealTimeLayerModule.Register` (called by `Startup` and the wasm host) registers the shared real-time services and finds `IWebSocketHandler`s and `IActionStepDefinition`s by scanning the RealTime assembly (`StartupDiscoveryTests` guards it); each host adds its own WebRTC stack, REST dispatcher and Central token refresh.

RealTime, Application, Infrastructure and Domain are what a browser-wasm host reaches, so none of them may reference ASP.NET Core, SIPSorcery, MySQL or SkiaSharp (`SharedAssemblyReferencesTests` guards it). The host brings those: `InfrastructureLayerModule.Register` takes the database used when `UseSqlite` is off (the native server passes `MySqlDatabase.Use` from `NordvikManager.Infrastructure.MySql`), and thumbnails go through `IThumbnailGenerator` (the native server registers `SkiaThumbnailGenerator` before `ApplicationLayerModule.Register`, which otherwise adds a no-thumbnails default that serves images at full size).

Two test projects: `NordvikManager.Tests` (controller/integration) and `NordvikManager.Application.UnitTests` (handler unit tests).

**Layer rules — strictly enforced:**
- `IDbContext` and EF Core queries belong ONLY in `NordvikManager.Application` command handlers (via `HandlerBase`). Never resolve or query `IDbContext` directly in `NordvikManager` or `NordvikManager.RealTime` (controllers, WebRTC dispatchers, WebSocket handlers, services).
- The UI/Presentation layer (`NordvikManager`) dispatches MediatR commands and reads DTOs — it never touches the database directly.
- When you need a new DB operation in a dispatcher or controller, create a Command + Handler pair in `Application/Commands/` and call `mediator.Send()`.

## Shared REST endpoints (NordvikManager.Api)

A browser-wasm GM host has no ASP.NET Core, so the REST requests players tunnel over the data channel are answered there by `ApiRouter` from shared endpoint classes (`NordvikManager.Api/Endpoints/`), instead of replaying them through the ASP.NET pipeline as the native server does. A controller moved to this pattern keeps its routes and attributes and only delegates: `(await endpoints.X(CurrentUser, ...)).ToActionResult()`. The endpoint method has the action's logic, returns `ApiResult` (`Ok`, `Unauthorized`, ... mapped by `ToActionResult` to the same MVC result types) and carries `[ApiRoute("GET", "api/Controller/Action")]`; a `User` parameter is the caller, simple parameters bind from the query (route values first; `[Required]` → 400), one complex one from the JSON body (`[ApiBody]` for a simple type from the body), `[ApiService]` from the services. An endpoint that needs cookies or the tunnel's game takes an `ApiContext` instead (`ReadCookie`, `SetCookie`, `Refreshed`, `TunnelGameId`): the controller makes it with `HttpContext.ToApiContext()` and answers with `HttpContext.Respond(context, result)`, which writes the cookie changes; a tunnelled request has no cookies and takes none back, as with the native tunnel.

- `ApiJson` is the JSON of both (Startup's `AddJsonOptions` calls `ApiJson.Configure`; `ApiJsonTests`); `ApiExceptions` maps exceptions for both (`HandleExceptionMiddleWare` uses it).
- `ApiRouteCoverageTests`: every action of a moved controller has an endpoint with its method and path, every endpoint is an action's; controllers not moved yet are listed there (`NotMigratedYet`) and answer 501 in the browser host. Not moved yet: Addon, Backup, Materials, Meta, ServerSettings, Update.

## CQRS with MediatR

All business logic lives in command handlers. Every command/handler pair follows this structure:

```csharp
// Command — extends CommandBase<TResponse>
public class AddGameCommand : CommandBase<bool>
{
    [JsonIgnore] public User? User { get; set; }
    [Required] public string Name { get; set; }
}

// Handler — extends HandlerBase<TCommand, TResponse>
internal class AddGameCommandHandler : HandlerBase<AddGameCommand, bool>
{
    public async override Task<bool> Handle(AddGameCommand request, CancellationToken cancellationToken)
    {
        await base.Handle(request, cancellationToken); // resolves dbContext from scope if set
        dbContext.Add(entity);
        dbContext.SaveChanges();
        return true;
    }
}
```

`HandlerBase` provides `dbContext` (IDbContext) and `mapper` (IMapper). Always call `await base.Handle(...)` first.

Handlers are auto-discovered by reflection in `ApplicationLayerModule.cs` — any class ending in `CommandHandler` is registered with MediatR automatically. Controllers dispatch via `_mediator.Send(command)`.

## Domain Entities

All entities implement `IEntity` (has `Guid Id`). Named entities also implement `INamedEntity`. Use Data Annotations for keys:

```csharp
public class GameModel : INamedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
}
```

## Permissions

Permissions are a bitwise enum: `None, Read, Execute, Control, Edit, Remove, All`.

```csharp
game.SetPermissions(player.Id, Permission.All);
game.ThrowIfNoPermission(playerId, Permission.Edit);  // throws if missing
bool ok = _permissionService.CheckIfHasPermissions(playerId, entity, Permission.Read);
```

## Mapping (Mapperly)

All DTO↔Model mappings are partial methods in `Application/Mapping/AppMapper.cs`, generated at compile time by [Mapperly](https://github.com/riok/mapperly). Handlers use the app's own `IMapper` (`Application/Mapping/IMapper.cs`, `mapper.Map<T>(source)`); lists map item by item, and null lists become empty ones. DTOs live in `Application/DataTransferObjects/`.

- A new pair: add a `private partial TTarget ToDto(TSource source)` / `ToModel(...)` method, plus the pair in `Application.UnitTests/Mapping/MappingSnapshotTests.cs` (then `UPDATE_MAPPING_SNAPSHOTS=1 dotnet test --filter MappingSnapshotTests` and review the new golden file).
- A target member nothing maps to fails the build (`RMG012`): map it (`[MapProperty]`, `[MapPropertyFromSource]`) or ignore it (`[MapperIgnoreTarget]`).
- Navigation properties are declared non-nullable but are null unless loaded: flatten them through a helper (`source.Game?.Id`), not `"Game.Id"`.

AutoMapper (commercial from v15) was replaced by this; the golden files in `Mapping/Snapshots/` pin the behaviour it had. **MediatR is pinned to 11.0.0** (`[11.0.0]` in the csproj files): 13+ is commercial and would need a license per installation, so don't upgrade it.

## Dependency Injection

Register new services in `ApplicationLayerModule.Register()` or `InfrastructureLayerModule.Register()`. WebSocket handlers and action step classes are auto-discovered via reflection — any class implementing `IWebSocketHandler` is registered automatically.

Use `AddScoped` for per-request, `AddSingleton` for cached/static data, `AddTransient` for lightweight per-instantiation.

## Authentication

JWT tokens are stored in **cookies** (not Authorization headers). The `OnMessageReceived` handler in Startup extracts the token from the request cookie. JWT secret is auto-generated at startup if not set in config.

**Central session.** Each game has a Central session (`Games.CentralSessionId`). `Services/CentralSessionSync.cs` keeps it in step on every session start: it creates one when the game has none, re-sends the listing including the game password otherwise, and creates a new one when Central answers that the session is gone (Central expires sessions whose GM stays away for `SESSION_EXPIRY_DAYS`). Only the game's GM or the local admin may create or replace it: whoever creates it owns it on Central and may connect to signaling as GM. Starting a session also joins the starting user to it on Central (`JoinSessionAsync`), because Central signaling admits only the owner and members as players. Deleting a game deletes its Central session. The WebRTC data channel refuses banned users (`CheckUserBannedCommand`) before resolving or auto-creating the player.

## Database

Controlled by `"UseSqlite": true` in appsettings. SQLite (`data.db`) is the default/primary path; MySQL is available as a production alternative (its provider is the `NordvikManager.Infrastructure.MySql` adapter, which `Startup` passes to `InfrastructureLayerModule.Register`). There is a single DbContext, `DndOneContext` (in `NordvikManager.Infrastructure/Data/Contexts/BattleMapContext.cs`).

**SQLite uses real EF Core Migrations** (`NordvikManager.Infrastructure/Migrations/`), applied automatically via `Database.Migrate()` once at startup (`Startup.Configure`). **Any change to an entity in `NordvikManager.Domain` needs a new migration before it reaches anyone's database** — schema changes no longer get picked up by just editing the model:

```bash
# from the repo root — dotnet-ef is a local tool (dotnet-tools.json), no global install needed
dotnet ef migrations add <DescriptiveName> --project NordvikManager.Infrastructure/NordvikManager.Infrastructure.csproj --startup-project NordvikManager/NordvikManager.csproj --context DndOneContext
```
Commit the generated files under `Migrations/`. Never hand-edit the database schema directly (no more ad-hoc `ALTER TABLE` shims) — always go through a migration.

**MySQL does not have migrations yet** — it still uses `Database.EnsureCreated()` (schema-from-model, never alters an existing table). Giving two relational providers real migrations in one assembly needs a separate migrations assembly/history table per provider, and that couldn't be verified without a live MySQL server. If MySQL sees real production use, that's the next piece to build — don't add MySQL migrations into the same `Migrations/` folder as SQLite's (a single migration-history scan can't tell which provider a migration belongs to).

The InMemory provider (unit tests, see `HandlerTestBase`) has no migrations support at all and keeps using `EnsureCreated()` — this is intentional and doesn't need touching when adding migrations.

Key appsettings entries:
- `FrontUrls:Client` — CORS origin (default `http://localhost:3000`)
- `JWT:ValidIssuer`, `JWT:ValidAudience`, `JWT:ExpireTime`
- `ConnectionStrings:DBData`
- `InitialAdminPassword` — seeded admin password on first run
- `AddonsConfiguration:MainRepository` — addon registry URL

## Protocol version

`NordvikManager/WebRTC/ProtocolVersion.cs` (`ProtocolVersion.Current`) is the player↔backend protocol number, sent to the Central Server in the GM's signaling `authenticate`. Central rejects backends below its `MIN_GM_PROTOCOL` (surfaced as `SignalingAuthException` → 409 `{ error }` from session start), and players are switched to the frozen player build for this number. Bump it only for changes an older player client can't handle, together with the frontend's `src/protocol.json` (CI checks they match).

## Testing Conventions

- Test method naming: `MethodName_ExpectedResult_Condition`
- Controller tests instantiate the real controller with mocked `IMediator` (Moq)
- Handler unit tests use EF Core InMemory database
- Use factory helpers like `SomePlayer()`, `AdminUser()` for test data setup

## Adding a New Feature (Checklist)

1. Entity model in `Domain/Entities/` implementing `IEntity` or `INamedEntity`
2. Command + Handler pair in `Application/Commands/{Feature}/`
3. DTO in `Application/DataTransferObjects/` + mapping in `Mapping/AppMapper.cs` (and its snapshot pair)
4. Controller action in `NordvikManager/Controllers/` dispatching via `_mediator.Send()`. For a controller already moved to shared endpoints (see below), the logic goes in its `NordvikManager.Api/Endpoints/*Endpoints` method with an `[ApiRoute]`, and the action only delegates
5. Tests in `Application.UnitTests/Commands/` (handler) and `NordvikManager.Tests/Controllers/` (controller)

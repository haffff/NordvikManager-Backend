# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, Test & Run

```bash
dotnet build                                                          # Build solution
dotnet run --project DNDOnePlaceManager                               # Run API (http://localhost:8213)

dotnet test                                                           # Run all tests
dotnet test DNDOnePlaceManager.Tests                                  # Integration/controller tests only
dotnet test DndOnePlaceManager.Application.UnitTests                  # Unit tests only
dotnet test --filter "Name~GetGames_ReturnsOk"                        # Single test by name
dotnet test --filter "ClassName=GameListControllerTests"              # Single test class
```

Frontend (separate repo) can be placed as a build in `wwwroot/` or run via `npm start` from the [frontend repo](https://github.com/haffff/NordvikManagerFrontEnd).

## Architecture

Clean Architecture with CQRS. Dependency flow is strictly one-directional:

```
DNDOnePlaceManager (API/Presentation, Microsoft.NET.Sdk.Web)
    → DndOnePlaceManager.RealTime  (GameLobby, ActionProcessingService, IWebSocketHandlers,
    │                               action-step engine, WebRTC/signaling interfaces + SignalingService)
    → DndOnePlaceManager.Application  (commands, handlers, DTOs, services)
    → DndOnePlaceManager.Infrastructure  (EF Core DbContext, storage, HTTP clients)
    → DndOnePlaceManager.Domain  (entities, interfaces, enums — no external deps)
```

`DndOnePlaceManager.RealTime` was extracted from `DNDOnePlaceManager` so a `browser-wasm`
project can reference it (a `Microsoft.NET.Sdk.Web` project cannot be referenced from the
`browser-wasm` RID). It has **zero `Microsoft.AspNetCore.*` references**. Keep it that way
when adding files. The browser-WASM GM host and the extraction constraints are documented
in [`docs/wasm-host.md`](docs/wasm-host.md).

Two test projects: `DNDOnePlaceManager.Tests` (controller/integration) and `DndOnePlaceManager.Application.UnitTests` (handler unit tests).

**Layer rules — strictly enforced:**
- `IDbContext` and EF Core queries belong ONLY in `DndOnePlaceManager.Application` command handlers (via `HandlerBase`). Never resolve or query `IDbContext` directly in `DNDOnePlaceManager` / `DndOnePlaceManager.RealTime` (controllers, WebRTC dispatchers, WebSocket handlers, services).
- The UI/Presentation layer (`DNDOnePlaceManager`) dispatches MediatR commands and reads DTOs — it never touches the database directly.
- When you need a new DB operation in a dispatcher or controller, create a Command + Handler pair in `Application/Commands/` and call `mediator.Send()`.

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

## AutoMapper

All DTO↔Model mappings are registered in `Application/AutoMapperProfile.cs`. DTOs live in `Application/DataTransferObjects/`.

## Dependency Injection

Register new services in `ApplicationLayerModule.Register()` or `InfrastructureLayerModule.Register()`. WebSocket handlers and action step classes are auto-discovered via reflection — the scans in `Startup.cs` target `typeof(IWebSocketHandler).Assembly` / `typeof(IActionStepDefinition).Assembly` (i.e. `DndOnePlaceManager.RealTime`), not the executing assembly.

Use `AddScoped` for per-request, `AddSingleton` for cached/static data, `AddTransient` for lightweight per-instantiation.

## Real-time transport

Players connect to the GM backend over **WebRTC data channels**, not a raw WebSocket. The Node `NordvikManager-Central` server does auth, the game-session registry, and WebRTC signaling relay (Socket.IO). `WebRTCSessionService` owns one peer connection per player; game commands go to `GameLobby.HandleCommand`, and `{ "type": "api-request" }` frames are tunneled REST via `WebRTCInProcessDispatcher` (native) — replayed through the ASP.NET Core pipeline. `SIPSorcery` provides the .NET-side peer connection (data channel only, no media).

## Authentication

User identity lives on the **Central Server** — `CentralServerService` (in `DndOnePlaceManager.RealTime`) calls its `/api/user/*` endpoints for login/register/refresh; the returned JWT is decoded locally (not signature-verified). The native host still runs `AddJwtBearer`, reading the token from the **`Authorization` cookie** (not the header) via `JwtBearerEvents.OnMessageReceived`; its signing key is `JWTSecret` from config or a random GUID generated at startup. ASP.NET Identity and the old `AuthContext` have been removed.

## Database

One EF Core `DbContext`: `DndOneContext` (file `DndOnePlaceManager.Infrastructure/Data/Contexts/BattleMapContext.cs`, namespace `DNDOnePlaceManager.Data.Contexts`), constructor calls `Database.EnsureCreated()`. Provider is chosen in `Startup.ConfigureServices`: `"UseSqlite": true` (default, `Data Source=.\data.db`) → `UseSqlite`, else `UseMySQL`. The **provider packages (`Microsoft.EntityFrameworkCore.Sqlite`, `MySql.EntityFrameworkCore`) live in the `DNDOnePlaceManager` project only** — not in `DndOnePlaceManager.Infrastructure` — so they stay out of the browser-wasm asset graph (see `docs/wasm-host.md`). `InfrastructureLayerModule.Register` no longer registers `IDbContext`; the caller does. Unit tests and the wasm host use EF Core InMemory.

Key appsettings entries:
- `FrontUrls:Client` — CORS origin (default `http://localhost:3000`)
- `JWT:ValidIssuer`, `JWT:ValidAudience`, `JWT:ExpireTime`
- `ConnectionStrings:DBData`
- `CentralServerUrl` — the Central Server base URL
- `InitialAdminPassword` — seeded admin password on first run
- `AddonsConfiguration:MainRepository` — addon registry URL

## Testing Conventions

- Test method naming: `MethodName_ExpectedResult_Condition`
- Controller tests instantiate the real controller with mocked `IMediator` (Moq)
- Handler unit tests use EF Core InMemory database
- Use factory helpers like `SomePlayer()`, `AdminUser()` for test data setup

## Adding a New Feature (Checklist)

1. Entity model in `Domain/Entities/` implementing `IEntity` or `INamedEntity`
2. Command + Handler pair in `Application/Commands/{Feature}/`
3. DTO in `Application/DataTransferObjects/` + mapping in `AutoMapperProfile.cs`
4. Controller action in `DNDOnePlaceManager/Controllers/` dispatching via `_mediator.Send()`
5. Tests in `Application.UnitTests/Commands/` (handler) and `DNDOnePlaceManager.Tests/Controllers/` (controller)

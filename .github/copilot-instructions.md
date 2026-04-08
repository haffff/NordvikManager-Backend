# Copilot Instructions — Nordvik Manager Backend

## Build, Test & Run

```bash
dotnet build                          # Build solution
dotnet run --project DNDOnePlaceManager  # Run API (http://localhost:8213)

dotnet test                           # Run all tests
dotnet test DNDOnePlaceManager.Tests  # Integration tests only
dotnet test DndOnePlaceManager.Application.UnitTests  # Unit tests only
dotnet test --filter "Name~GetGames_ReturnsOk"        # Single test by name
dotnet test --filter "ClassName=GameListControllerTests"  # Single class
```

## Architecture

Clean Architecture with CQRS. Dependency flow is strictly one-directional:

```
DNDOnePlaceManager (API/Presentation)
    → DndOnePlaceManager.Application  (commands, handlers, DTOs, services)
    → DndOnePlaceManager.Infrastructure  (EF Core DbContexts, repos, HTTP clients)
    → DndOnePlaceManager.Domain  (entities, interfaces, enums — no external deps)
```

Two test projects: `DNDOnePlaceManager.Tests` (controller/integration) and `DndOnePlaceManager.Application.UnitTests` (handler unit tests).

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

Handlers are auto-discovered by reflection in `ApplicationLayerModule.cs` — any class ending in `CommandHandler` is registered with MediatR automatically.

Controllers dispatch commands via `_mediator.Send(command)` and return the result directly.

## Domain Entities

All entities implement `IEntity` (has `Guid Id`). Named entities also implement `INamedEntity`. Use Data Annotations for keys and generation:

```csharp
public class GameModel : INamedEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    // ...
}
```

## Permissions

Permissions are a bitwise enum: `None, Read, Execute, Control, Edit, Remove, All`.

```csharp
// Set
game.SetPermissions(player.Id, Permission.All);

// Enforce (throws if missing)
game.ThrowIfNoPermission(playerId, Permission.Edit);

// Check
bool ok = _permissionService.CheckIfHasPermissions(playerId, entity, Permission.Read);
```

## AutoMapper

All DTO↔Model mappings are registered in `Application/AutoMapperProfile.cs`. Add new mappings there. DTOs live in `Application/DataTransferObjects/`.

## Dependency Injection

Register new services in `ApplicationLayerModule.Register()` or `InfrastructureLayerModule.Register()`. WebSocket handlers and action step classes are auto-discovered via reflection — any class implementing `IWebSocketHandler` is registered automatically.

Use `AddScoped` for per-request services, `AddSingleton` for cached/static data, `AddTransient` for lightweight per-instantiation services.

## Authentication

JWT tokens are stored in **cookies** (not Authorization headers). The `OnMessageReceived` handler in Startup extracts the token from the request cookie. JWT secret is auto-generated at startup if not set in config.

## Database

Controlled by `"UseSqlite": true` in appsettings. SQLite (two files: `data.db`, `usersdata.db`) is the default. MySQL is available for production. Two DbContexts: `DndOneContext` (app data) and `AuthContext` (ASP.NET Identity). Both call `Database.EnsureCreated()` on construction.

## Configuration

Key appsettings entries:
- `FrontUrls:Client` — CORS origin (default `http://localhost:3000`)
- `JWT:ValidIssuer`, `JWT:ValidAudience`, `JWT:ExpireTime`
- `ConnectionStrings:DBData` / `ConnectionStrings:AuthData`
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

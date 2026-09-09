using DndOnePlaceManager.Application;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NordvikManager.Wasm.Host;
using NordvikManager.Wasm.Host.Harness;
using NordvikManager.Wasm.Host.RealSession;
using NordvikManager.Wasm.Host.Rest;

// Two entry paths share this one compiled assembly: index.html/main.js (harness suite,
// TestSignalingService) vs gm.html (real GM session, real SignalingService against a
// real Central Server) — see RealSession/HostMode.cs and RealSession/RealGmSession.cs.
var isRealSession = await HostMode.IsRealSessionAsync();

// InfrastructureLayerModule.Register isn't called here — it unconditionally wires
// UseSqlite/UseMySQL from config, and EF Core throws if two providers get registered
// for the same DbContext. IDbContext is registered by hand below instead, same
// InMemory pattern proven in the earlier DI/MediatR-only POC.
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        // SignalingService isn't wired into this container's DI yet (TestSignalingService
        // stands in for it — see WebRTCSessionServiceHarness), but this flag is required
        // for it to work at all under wasm (see SignalingService.cs's comment on the
        // field), so it's set here ready for whenever that swap happens.
        ["Signaling:ForceWebSocketTransport"] = "true",
    })
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration); // WebRTCSessionService/CentralServerService resolve IConfiguration via DI, not the local variable
// AddConsole() throws PlatformNotSupportedException under browser-wasm (terminal
// detection) — SimpleWasmLoggerProvider just writes to Console.Out instead. Without
// any provider, LogError/LogWarning calls silently produce no output at all.
services.AddLogging(builder => builder.AddProvider(new SimpleWasmLoggerProvider()));
services.AddHttpClient(); // required by CentralServerService's IHttpClientFactory
ApplicationLayerModule.Register(services, configuration);
services.AddDbContext<IDbContext, DndOneContext>(o => o.UseInMemoryDatabase("wasm-host"));

services.AddScoped<IGameEventLogger, LobbyGameEventLogger>();
services.AddScoped<IActionProcessingService, ActionProcessingService>();
services.AddSingleton<ILobbyRegistry, LobbyRegistry>();
// Satisfies DI for handlers whose constructor takes IFileStorageProvider (e.g.
// CreateResourceCommandHandler) — see WasmFileStorageProvider's own comment for why the
// real LocalFileStorageProvider can't be used here and why this stub is safe regardless.
services.AddSingleton<IFileStorageProvider, WasmFileStorageProvider>();

// CentralServerService is the real implementation in both modes — harmless if
// unreachable in harness mode, WebRTCSessionService already falls back to default STUN
// when GetMetaAsync() returns null. ISignalingService is the one thing that differs:
// the harness suite fakes it (TestSignalingService — no real Central Server, "peer
// joined" injected directly by the harness); the real GM session (gm.html) uses the
// real SocketIOClient-based SignalingService against a real Central Server.
services.AddSingleton<ICentralServerService, CentralServerService>();
if (isRealSession)
    services.AddSingleton<ISignalingService, SignalingService>();
else
    services.AddSingleton<ISignalingService, TestSignalingService>();
services.AddSingleton<IWebRTCApiDispatcher, WebRTCRestDispatcher>();
services.AddSingleton<IWebRTCSessionService, WebRTCSessionService>();

// REST-tunnel handlers (read-only game-load slice) — same reflection-scan pattern as
// IWebSocketHandler below, resolved fresh per request by WebRTCRestDispatcher.
foreach (var restHandlerType in typeof(IWebRTCRestHandler).Assembly.GetTypes())
    if (restHandlerType.GetInterface(nameof(IWebRTCRestHandler)) != null)
        services.AddScoped(typeof(IWebRTCRestHandler), restHandlerType);

// Same retargeted-assembly-scan fix applied in Startup.cs — these implementations
// live in DndOnePlaceManager.RealTime, not this executable's own assembly.
foreach (var handlerType in typeof(IWebSocketHandler).Assembly.GetTypes())
    if (handlerType.GetInterface(nameof(IWebSocketHandler)) != null)
        services.AddScoped(typeof(IWebSocketHandler), handlerType);

foreach (var stepType in typeof(IActionStepDefinition).Assembly.GetTypes())
    if (stepType.GetInterface(nameof(IActionStepDefinition)) != null)
        services.AddSingleton(typeof(IActionStepDefinition), stepType);

var provider = services.BuildServiceProvider();
ApplicationLayerModule.AfterBuild(provider);

if (isRealSession)
{
    Console.WriteLine("[Host] Running RealGmSession (real Central Server login/session/signaling)...");
    await RealGmSession.RunAsync(provider);
}
else
{
    // Each harness records its own pass/fail via HarnessReport; the aggregate is posted
    // once in the finally (HarnessReport.FlushAsync also calls window.close()), so it
    // survives even if a harness throws.
    try
    {
        Console.WriteLine("[Host] Running GameLobbyHarness against the extracted DndOnePlaceManager.RealTime layer...");
        await GameLobbyHarness.RunAsync(provider);

        Console.WriteLine("[Host] Running CentralSignalingSpike (real SignalingService against the local Central Server)...");
        await CentralSignalingSpike.RunAsync(provider);

        Console.WriteLine("[Host] Running WebRTCSessionServiceHarness (the real WebRTCSessionService, end to end)...");
        await WebRTCSessionServiceHarness.RunAsync(provider);

        Console.WriteLine("[Host] Running SessionServiceHarness against NordvikManager.Wasm.SipWrapper...");
        await SessionServiceHarness.RunAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Host] Harness suite threw: {ex}");
        HarnessReport.Record("HarnessSuite", false, $"unhandled exception: {ex.Message}");
    }
    finally
    {
        Console.WriteLine("[Host] Harness finished — posting aggregate result.");
        await HarnessReport.FlushAsync();
    }
}

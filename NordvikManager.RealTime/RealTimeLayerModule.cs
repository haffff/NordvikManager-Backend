using Microsoft.Extensions.DependencyInjection;
using NordvikManager.Services;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets.Handlers;

namespace NordvikManager
{
    /// <summary>
    /// The real-time layer's services, the same in every GM host (the native server and the
    /// browser-wasm host). Each host adds what is its own: the WebRTC stack
    /// (<see cref="IPeerConnectionFactory"/>), how tunnelled REST requests are answered
    /// (<see cref="IWebRTCApiDispatcher"/>) and how Central tokens are refreshed.
    /// </summary>
    public static class RealTimeLayerModule
    {
        public static void Register(IServiceCollection services)
        {
            services.AddScoped<IWebSocketTokenValidator, WebSocketTokenValidator>();
            services.AddScoped<NordvikManager.Application.Interfaces.IGameEventLogger, LobbyGameEventLogger>();
            services.AddScoped<ILobbyService, LobbyService>();
            services.AddSingleton<IPlaybackService, PlaybackService>();
            services.AddScoped<IActionProcessingService, ActionProcessingService>();
            services.AddSingleton<ILobbyRegistry, LobbyRegistry>();
            services.AddSingleton<ISignalingService, SignalingService>();
            services.AddSingleton<IWebRTCSessionService, WebRTCSessionService>();

            // WebSocket-command handlers and action steps live in this assembly, found by reflection.
            foreach (var type in typeof(RealTimeLayerModule).Assembly.GetTypes())
            {
                if (type.GetInterface(nameof(IWebSocketHandler)) != null)
                    services.AddScoped(typeof(IWebSocketHandler), type);
                if (type.GetInterface(nameof(IActionStepDefinition)) != null)
                    services.AddSingleton(typeof(IActionStepDefinition), type);
            }
        }
    }
}

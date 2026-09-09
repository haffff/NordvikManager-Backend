using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest
{
    /// <summary>
    /// Real IWebRTCApiDispatcher implementation for the wasm host, replacing
    /// NotImplementedApiDispatcher. Can't reuse the real backend's
    /// WebRTCInProcessDispatcher (tunnels into the real ASP.NET Core middleware
    /// pipeline — confirmed unavailable under browser-wasm, no runtime pack for
    /// Microsoft.AspNetCore.App on the browser-wasm RID) so this hand-rolls
    /// {method,path,body} -> mediator.Send(), mirroring GameLobby's
    /// IWebSocketHandler dispatch loop.
    ///
    /// Unlike GameLobby (which resolves its handlers once from a lobby-lifetime scope),
    /// this creates a fresh scope per request so each REST call gets its own scoped
    /// IMediator/DbContext rather than one living for the whole WebRTC session.
    /// </summary>
    public class WebRTCRestDispatcher : IWebRTCApiDispatcher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<WebRTCRestDispatcher> _logger;

        public WebRTCRestDispatcher(IServiceScopeFactory scopeFactory, ILogger<WebRTCRestDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task DispatchAsync(WebRTCApiRequest request, PlayerDTO player, Guid gameId, IPlayerConnection connection)
        {
            var method = (request.Method ?? "GET").ToUpperInvariant();
            var path = (request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant();

            WebRTCApiResult? result;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handlers = scope.ServiceProvider.GetServices<IWebRTCRestHandler>();

                result = null;
                foreach (var handler in handlers)
                {
                    result = await handler.Handle(request, player, gameId);
                    if (result != null)
                        break;
                }

                if (result == null)
                {
                    _logger.LogWarning("Unmapped WebRTC REST route: {Method} {Path}", method, path);
                    result = new WebRTCApiResult(501, new { error = $"No REST-tunnel mapping for {method} {path}" });
                }
            }
            catch (Exception ex)
            {
                // DispatchAsync is invoked fire-and-forget from WebRTCSessionService's
                // onmessage handler — an unhandled exception here would escape into an
                // unobserved task and the caller's pending request would hang until its
                // own timeout instead of getting a response. Match
                // WebRTCInProcessDispatcher's real-backend behavior: turn it into a 500.
                _logger.LogError(ex, "WebRTC REST dispatch failed for {Method} {Path}", method, path);
                result = new WebRTCApiResult(500, new { error = ex.Message });
            }

            await connection.SendMessageToPlayer(new
            {
                type = "api-response",
                id = request.Id,
                status = result.Status,
                body = result.Body
            });
        }
    }
}

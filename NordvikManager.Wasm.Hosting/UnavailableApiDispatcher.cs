using Microsoft.Extensions.Logging;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.WebRTC;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The REST calls players tunnel over the data channel, until the browser host has its own
    /// dispatcher (the native one replays them through ASP.NET Core, which a browser lacks):
    /// each gets a 501 answer, so the player client fails the request instead of waiting.
    /// </summary>
    public sealed class UnavailableApiDispatcher : IWebRTCApiDispatcher
    {
        public const string Message = "This request isn't available yet in games hosted in a browser.";

        private readonly ILogger<UnavailableApiDispatcher> _logger;

        public UnavailableApiDispatcher(ILogger<UnavailableApiDispatcher> logger) => _logger = logger;

        public async Task DispatchAsync(WebRTCApiRequest request, PlayerDTO player, Guid gameId, IPlayerConnection connection)
        {
            _logger.LogWarning("Not available in the browser host yet: {Method} {Path} from {Player}", request.Method, request.Path, player.Name);
            await connection.SendMessageToPlayer(new
            {
                type = "api-response",
                id = request.Id,
                status = 501,
                body = new { error = Message },
            });
        }
    }
}

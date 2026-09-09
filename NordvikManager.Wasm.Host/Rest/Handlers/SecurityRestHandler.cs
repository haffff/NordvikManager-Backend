using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using MediatR;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>Ported from DNDOnePlaceManager/Controllers/SecurityController.cs.</summary>
    public class SecurityRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;

        public SecurityRestHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return null;

            if ((request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant() != "api/security/permissions")
                return null;

            Guid.TryParse(request.Query.Get("entityId"), out var entityId);
            var result = await _mediator.Send(new GetPermissionsCommand { EntityId = entityId, Player = player });
            return new WebRTCApiResult(200, result);
        }
    }
}

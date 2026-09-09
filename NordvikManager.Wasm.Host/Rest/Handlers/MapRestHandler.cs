using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Rest.Handlers
{
    /// <summary>
    /// Ported from DNDOnePlaceManager/Controllers/MapController.cs. Note the real route for
    /// GetMap is "Get", not "GetMap" — [Route("Get")] on the action, so the full path is
    /// api/map/get.
    /// </summary>
    public class MapRestHandler : IWebRTCRestHandler
    {
        private readonly IMediator _mediator;

        public MapRestHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<WebRTCApiResult?> Handle(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                return null;

            switch ((request.Path ?? string.Empty).TrimStart('/').ToLowerInvariant())
            {
                case "api/map/get":
                {
                    Guid.TryParse(request.Query.Get("mapId"), out var mapId);
                    var result = await _mediator.Send(new GetMapCommand { Id = mapId, Player = player });
                    return new WebRTCApiResult(200, result);
                }

                case "api/map/getallflat":
                {
                    var result = await _mediator.Send(new GetFlatMapsCommand { GameID = gameId, Player = player });
                    return new WebRTCApiResult(200, result);
                }

                default:
                    return null;
            }
        }
    }
}

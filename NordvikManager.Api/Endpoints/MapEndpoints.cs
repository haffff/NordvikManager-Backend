using MediatR;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Map.GetFlatMaps;
using NordvikManager.Application.Commands.Map.GetMap;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Map (MapController delegates here).</summary>
    public sealed class MapEndpoints
    {
        private readonly IMediator mediator;

        public MapEndpoints(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [ApiRoute("GET", "api/Map/Get")]
        public async Task<ApiResult> GetMap(User? user, Guid mapId, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });

            var res = await mediator.Send(new GetMapCommand { Id = mapId, Player = player.Player });
            return ApiResult.Ok(res);
        }

        [ApiRoute("GET", "api/Map/GetAllFlat")]
        public async Task<ApiResult> GetAllFlat(User? user, Guid gameId)
        {
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

            var res = await mediator.Send(new GetFlatMapsCommand { GameID = gameId, Player = player.Player });
            return ApiResult.Ok(res);
        }
    }
}

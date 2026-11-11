using MediatR;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.TurnOrder;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/TurnOrder: reading a map's turn order (TurnOrderController delegates here).</summary>
    public sealed class TurnOrderEndpoints
    {
        private readonly IMediator mediator;

        public TurnOrderEndpoints(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>The map's turn order: hidden entries left out unless the player can edit the map.</summary>
        [ApiRoute("GET", "api/TurnOrder")]
        public async Task<ApiResult> Get(User? user, Guid gameId, Guid mapId)
        {
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

            try
            {
                return ApiResult.Ok(await mediator.Send(new GetTurnOrderCommand { GameId = gameId, MapId = mapId, Player = player.Player }));
            }
            catch (PermissionException)
            {
                return ApiResult.Forbid();
            }
            catch (ResourceNotFoundException)
            {
                return ApiResult.NotFound();
            }
        }
    }
}

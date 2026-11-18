using MediatR;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Operation: the state of a long-running operation (OperationController delegates here).</summary>
    public sealed class OperationEndpoints
    {
        private readonly IMediator mediator;

        public OperationEndpoints(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// { status: running | complete | failed | unknown, title, description }. Unknown: not this
        /// player's operation, or forgotten (an hour on, or a server restart).
        /// </summary>
        [ApiRoute("GET", "api/Operation/status")]
        public async Task<ApiResult> Status(User? user, Guid gameId, Guid id)
        {
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });
            if (player?.Player == null)
                return ApiResult.Unauthorized(new { error = "You are not a player in this game" });

            var entry = OperationResults.Shared.Get(id, player.Player.Id ?? default);
            if (entry == null)
                return ApiResult.Ok(new { status = "unknown" });
            return ApiResult.Ok(new { status = entry.Status, title = entry.Title, description = entry.Description });
        }
    }
}

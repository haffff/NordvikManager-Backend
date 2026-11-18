using MediatR;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Security.GetPermissions;
using NordvikManager.Controllers;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Infrastructure.Services.Sandbox;

namespace NordvikManager.Api.Endpoints
{
    /// <summary>api/Security (SecurityController delegates here).</summary>
    public sealed class SecurityEndpoints
    {
        private readonly IMediator mediator;

        public SecurityEndpoints(IMediator mediator)
        {
            this.mediator = mediator;
        }

        /// <summary>
        /// The hosts addon cards and Html chat messages may load from
        /// (Security:SandboxAllowedOrigins). Player clients enforce it themselves.
        /// </summary>
        [ApiRoute("GET", "api/Security/sandboxorigins")]
        public ApiResult GetSandboxOrigins([ApiService] SandboxOrigins origins)
            => ApiResult.Ok(new SandboxOriginsResponse { Origins = origins.Origins });

        /// <summary>An entity's permissions.</summary>
        [ApiRoute("GET", "api/Security/permissions")]
        public async Task<ApiResult> GetPermissions(User? user, Guid gameId, Guid entityId)
        {
            var player = await mediator.Send(new GetPlayerCommand { User = user, GameID = gameId });

            var res = await mediator.Send(new GetPermissionsCommand { EntityId = entityId, Player = player.Player });
            return ApiResult.Ok(res);
        }
    }
}

using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Soundboard.PlaySound
{
    internal class PlaySoundCommandHandler : HandlerBase<PlaySoundCommand, CommandResponse>
    {
        private readonly IPermissionService permissionService;

        public PlaySoundCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<CommandResponse> Handle(PlaySoundCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            var resourceExists = await dbContext.Resources
                .AnyAsync(r => r.Id == request.ResourceId && r.GameId == request.GameId, cancellationToken);

            if (!resourceExists)
                return CommandResponse.NoResource;

            return CommandResponse.Ok;
        }
    }
}

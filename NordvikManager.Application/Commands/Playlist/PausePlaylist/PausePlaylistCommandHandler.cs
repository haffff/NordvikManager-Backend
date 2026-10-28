using NordvikManager.Application.Mapping;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Playlist.PausePlaylist
{
    internal class PausePlaylistCommandHandler : HandlerBase<PausePlaylistCommand, CommandResponse>
    {
        private readonly IPermissionService permissionService;

        public PausePlaylistCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<CommandResponse> Handle(PausePlaylistCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            return CommandResponse.Ok;
        }
    }
}

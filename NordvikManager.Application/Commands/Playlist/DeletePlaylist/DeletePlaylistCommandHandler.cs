using NordvikManager.Application.Mapping;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Guards;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.Commands.Playlist.DeletePlaylist
{
    internal class DeletePlaylistCommandHandler : HandlerBase<DeletePlaylistCommand, CommandResponse>
    {
        private readonly IPermissionService permissionService;
        private readonly IMediator mediator;

        public DeletePlaylistCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService, IMediator mediator)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
            this.mediator = mediator;
        }

        public override async Task<CommandResponse> Handle(DeletePlaylistCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            var playlist = await dbContext.Playlists
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.GameId == request.GameId, cancellationToken);

            Guard.NotFound(playlist, "Playlist", request.PlaylistId);

            dbContext.Remove(playlist);

            var result = dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;

            // A missing entry (playlists made before they had folders) is a no-op.
            await mediator.Send(new RemoveTreeEntryCommand
            {
                TargetId = playlist.Id,
                GameId = request.GameId,
                PlayerId = playerId,
            }, cancellationToken);

            return result;
        }
    }
}

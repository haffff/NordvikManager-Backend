using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Playlist.UpdatePlaylist
{
    internal class UpdatePlaylistCommandHandler : HandlerBase<UpdatePlaylistCommand, CommandResponse>
    {
        private readonly IPermissionService permissionService;

        public UpdatePlaylistCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<CommandResponse> Handle(UpdatePlaylistCommand request, CancellationToken cancellationToken)
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
                .Include(p => p.Resources)
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.GameId == request.GameId, cancellationToken);

            Guard.NotFound(playlist, "Playlist", request.PlaylistId);

            var resources = await dbContext.Resources
                .Where(r => request.ResourceIds.Contains(r.Id) && r.GameId == request.GameId)
                .ToListAsync(cancellationToken);

            playlist.Name = request.Name;
            playlist.Description = request.Description;
            playlist.Mode = request.Mode;
            playlist.Shuffle = request.Shuffle;
            playlist.Repeat = request.Repeat;
            playlist.Kind = request.Kind;
            playlist.Resources = resources;

            return dbContext.SaveChanges() > 0 ? CommandResponse.Ok : CommandResponse.NoChange;
        }
    }
}

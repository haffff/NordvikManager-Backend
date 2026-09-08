using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Playlist.PlayPlaylist
{
    internal class PlayPlaylistCommandHandler : HandlerBase<PlayPlaylistCommand, PlayPlaylistResult>
    {
        private readonly IPermissionService permissionService;

        public PlayPlaylistCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<PlayPlaylistResult> Handle(PlayPlaylistCommand request, CancellationToken cancellationToken)
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

            var order = playlist.Resources.Select(r => r.Id).ToList();

            if (order.Count == 0)
                return new PlayPlaylistResult { Response = CommandResponse.NoResource };

            // Shuffle is meaningful only in Sequential mode — Concurrent always keeps DB list order.
            if (playlist.Mode == PlaybackMode.Sequential && playlist.Shuffle)
                order = PlaylistShuffleHelper.FisherYatesShuffle(order);

            return new PlayPlaylistResult
            {
                Response = CommandResponse.Ok,
                Mode = playlist.Mode,
                Shuffle = playlist.Shuffle,
                Repeat = playlist.Repeat,
                TrackOrder = order,
            };
        }
    }
}

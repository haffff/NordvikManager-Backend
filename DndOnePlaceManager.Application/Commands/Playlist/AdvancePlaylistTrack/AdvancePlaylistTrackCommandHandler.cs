using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Playlist.AdvancePlaylistTrack
{
    internal class AdvancePlaylistTrackCommandHandler : HandlerBase<AdvancePlaylistTrackCommand, AdvancePlaylistTrackResult>
    {
        private readonly IPermissionService permissionService;

        public AdvancePlaylistTrackCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<AdvancePlaylistTrackResult> Handle(AdvancePlaylistTrackCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            // Re-read the canonical resource-id list from the DB (not the possibly-stale
            // client-supplied order) — so a playlist edited mid-play picks up track
            // additions/removals on its next lap wrap, for free.
            var playlist = await dbContext.Playlists
                .Include(p => p.Resources)
                .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.GameId == request.GameId, cancellationToken);

            Guard.NotFound(playlist, "Playlist", request.PlaylistId);

            var nextIndex = request.CurrentTrackIndex + 1;

            if (nextIndex < request.CurrentTrackOrder.Count)
            {
                return new AdvancePlaylistTrackResult
                {
                    Response = CommandResponse.Ok,
                    Ended = false,
                    NextTrackOrder = request.CurrentTrackOrder,
                    NextTrackIndex = nextIndex,
                    NextTrackId = request.CurrentTrackOrder[nextIndex],
                };
            }

            var canonicalIds = playlist.Resources.Select(r => r.Id).ToList();

            if (!request.Repeat || canonicalIds.Count == 0)
            {
                return new AdvancePlaylistTrackResult { Response = CommandResponse.Ok, Ended = true };
            }

            // Repeat == true: start a new lap. Shuffle-per-lap — a fresh shuffle every wrap,
            // not one fixed shuffle reused forever.
            var newOrder = request.Shuffle
                ? PlaylistShuffleHelper.FisherYatesShuffle(canonicalIds)
                : canonicalIds;

            return new AdvancePlaylistTrackResult
            {
                Response = CommandResponse.Ok,
                Ended = false,
                NextTrackOrder = newOrder,
                NextTrackIndex = 0,
                NextTrackId = newOrder[0],
            };
        }
    }
}

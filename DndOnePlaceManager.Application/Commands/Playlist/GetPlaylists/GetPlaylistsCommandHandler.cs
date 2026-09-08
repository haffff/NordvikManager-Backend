using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists
{
    internal class GetPlaylistsCommandHandler : HandlerBase<GetPlaylistsCommand, List<PlaylistDTO>>
    {
        private readonly IPermissionService permissionService;

        public GetPlaylistsCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
        }

        public override async Task<List<PlaylistDTO>> Handle(GetPlaylistsCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            var playlists = await dbContext.Playlists
                .Include(p => p.Resources)
                .Where(p => p.GameId == request.GameId && p.Kind == request.Kind)
                .ToListAsync(cancellationToken);

            // Path can reveal the backend host's absolute filesystem layout for
            // ManagedFile/Linked resources — GM-only, mirrors GetResourcesCommandHandler.
            // The client doesn't need it to play a track anyway.
            var canSeeAll = request.Player?.System == true || game.MasterId == playerId;

            return playlists.Select(p => MapPlaylist(p, canSeeAll)).ToList();
        }

        private PlaylistDTO MapPlaylist(Domain.Entities.PlaylistModel playlist, bool canSeeAll)
        {
            var dto = mapper.Map<PlaylistDTO>(playlist);
            dto.Resources = playlist.Resources.Select(r =>
            {
                var resourceDto = mapper.Map<ResourceDTO>(r);
                resourceDto.Data = null;
                if (!canSeeAll)
                    resourceDto.Path = null;
                return resourceDto;
            }).ToList();
            return dto;
        }
    }
}

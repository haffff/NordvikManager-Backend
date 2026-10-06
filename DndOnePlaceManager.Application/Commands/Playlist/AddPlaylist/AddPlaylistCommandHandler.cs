using AutoMapper;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Playlist.AddPlaylist
{
    internal class AddPlaylistCommandHandler : HandlerBase<AddPlaylistCommand, (CommandResponse, Guid)>
    {
        private readonly IPermissionService permissionService;
        private readonly IMediator mediator;

        public AddPlaylistCommandHandler(IDbContext dbContext, IMapper mapper, IPermissionService permissionService, IMediator mediator)
            : base(dbContext, mapper)
        {
            this.permissionService = permissionService;
            this.mediator = mediator;
        }

        public override async Task<(CommandResponse, Guid)> Handle(AddPlaylistCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);

            Guard.NotFound(game, "Game", request.GameId);

            if (!permissionService.CheckIfHasPermissions(playerId, game, Permission.Edit))
                throw new PermissionException(Permission.Edit);

            var resources = await dbContext.Resources
                .Where(r => request.ResourceIds.Contains(r.Id) && r.GameId == request.GameId)
                .ToListAsync(cancellationToken);

            var model = new Domain.Entities.PlaylistModel
            {
                GameId      = request.GameId,
                Name        = request.Name,
                Description = request.Description,
                Mode        = request.Mode,
                Shuffle     = request.Shuffle,
                Repeat      = request.Repeat,
                Kind        = request.Kind,
                Resources   = resources,
            };

            await dbContext.Playlists.AddAsync(model, cancellationToken);
            dbContext.SaveChanges();

            // Playlists and soundboards each have their own folder tree in their panel.
            await mediator.Send(new AddTreeEntryCommand
            {
                TreeEntryDto = new TreeEntryDto
                {
                    Name = model.Name,
                    EntryType = TreeEntryTypes.ForPlaylist(model.Kind),
                    IsFolder = false,
                    TargetId = model.Id,
                },
                GameId = request.GameId,
                Player = request.Player,
            }, cancellationToken);

            return (CommandResponse.Ok, model.Id);
        }
    }
}

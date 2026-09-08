using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.Transfer
{
    internal class TransferResourceStorageCommandHandler : HandlerBase<TransferResourceStorageCommand, CommandResponse>
    {
        private readonly IFileStorageProvider storage;

        public TransferResourceStorageCommandHandler(IDbContext dbContext, IMapper mapper, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            this.storage = storage;
        }

        public override async Task<CommandResponse> Handle(TransferResourceStorageCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (request.TargetStorage == ResourceStorageKind.Linked)
                throw new WrongArgumentsException(nameof(request.TargetStorage));

            var playerId = request.Player?.Id ?? Guid.Empty;

            var game = await dbContext.Games
                .Include(g => g.Players)
                .FirstOrDefaultAsync(g => g.Id == request.GameId && g.Players.Any(p => p.Id == playerId), cancellationToken);
            Guard.NotFound(game, "Game", request.GameId);

            var player = await dbContext.Players.FirstOrDefaultAsync(p => p.Id == playerId, cancellationToken);
            Guard.NotFound(player, "Player", playerId);

            var isGM = player.System || game.MasterId == playerId;
            if (!isGM)
                throw new PermissionException(Permission.Edit);

            var resource = await dbContext.Resources
                .FirstOrDefaultAsync(r => r.Id == request.ResourceId && r.GameId == request.GameId, cancellationToken);
            Guard.NotFound(resource, "Resource", request.ResourceId);

            if (resource.Storage == request.TargetStorage)
                return CommandResponse.NoChange;

            var bytes = resource.Storage == ResourceStorageKind.Blob
                ? resource.Data
                : await storage.ReadAsync(resource.Path);

            if (bytes == null)
                throw new ResourceNotFoundException("Resource", request.ResourceId);

            var previousStorage = resource.Storage;
            var previousPath = resource.Path;

            if (request.TargetStorage == ResourceStorageKind.Blob)
            {
                resource.Data = bytes;
                resource.Path = null;
                resource.Storage = ResourceStorageKind.Blob;
            }
            else
            {
                resource.Path = await storage.SaveAsync(resource.GameId, resource.Id, bytes, null);
                resource.Data = null;
                resource.Storage = ResourceStorageKind.ManagedFile;
            }

            dbContext.SaveChanges();

            // Only ever delete the OLD file if it was a ManagedFile we owned — a Linked
            // source's original file must never be touched. This is how "adopt" works: the
            // GM's original file survives a Linked -> ManagedFile/Blob transfer untouched.
            if (previousStorage == ResourceStorageKind.ManagedFile && previousPath != null)
                await storage.DeleteAsync(previousPath);

            return CommandResponse.Ok;
        }
    }
}

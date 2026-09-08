
using AutoMapper;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources
{
    public class RemoveResourceCommandHandler : HandlerBase<RemoveResourceCommand, CommandResponse>
    {
        private readonly IMediator mediator;
        private readonly IFileStorageProvider storage;

        public RemoveResourceCommandHandler(IDbContext battleMapContext, IMapper mapper, IMediator mediator, IFileStorageProvider storage) : base(battleMapContext, mapper)
        {
            this.mediator = mediator;
            this.storage = storage;
        }

        public override async Task<CommandResponse> Handle(RemoveResourceCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var image = await dbContext.Resources.FirstOrDefaultAsync(x =>
                (request.ID.HasValue && x.Id == request.ID) ||
                (!string.IsNullOrWhiteSpace(request.Key) && x.Key == request.Key && x.GameId == request.GameId));

            if (image.PlayerId != request.Player.Id && !(request.Player.IsOwner ?? false))
            {
                throw new PermissionException(Permission.Edit);
            }

            Guard.Argument(image.GameId == request.GameId, nameof(request.GameId));

            if (image != null)
            {
                // Only a managed file is ours to delete — after the DB row is gone, so a failed
                // SaveChanges doesn't orphan a DB row pointing at nothing. A linked resource's
                // real file is never touched here: this codepath serves both "delete" (WS
                // resource_delete, used by MaterialsPanel's delete button) and "unlink" (same
                // button, relabeled client-side for linked resources) — they differ only in
                // whether Storage says there's a file we own.
                var pathToDelete = image.Storage == ResourceStorageKind.ManagedFile ? image.Path : null;

                dbContext.Remove(image);
                Guard.Argument(dbContext.SaveChanges() > 0, nameof(request.ID));

                if (pathToDelete != null)
                    await storage.DeleteAsync(pathToDelete);

                var foundEntries = dbContext.TreeEntries.Where(x => x.TargetId == request.ID).ToList();

                foreach (var item in foundEntries)
                {
                    RemoveTreeEntryCommand removeTreeEntryCommand = new RemoveTreeEntryCommand()
                    {
                        GameId = request.GameId,
                        PlayerId = request.Player.Id,
                        TreeEntryId = item.Id,
                        TargetId = image.Id
                    };

                    await mediator.Send(removeTreeEntryCommand);
                }

                return CommandResponse.Ok;
            }

            throw new ResourceNotFoundException("Resource", (object?)request.ID ?? request.Key);
        }
    }
}


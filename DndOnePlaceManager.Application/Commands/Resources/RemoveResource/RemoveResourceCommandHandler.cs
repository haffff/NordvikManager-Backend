
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

            // Bug fix: this used to be dereferenced (image.PlayerId) before any null check, so a
            // not-found resource crashed with NullReferenceException instead of reaching
            // ResourceNotFoundException below (see RemoveResourceCommandHandlerTests' formerly
            // "_KnownBug" test, now updated to expect the real exception).
            if (image == null)
                throw new ResourceNotFoundException("Resource", (object?)request.ID ?? request.Key);

            if (image.PlayerId != request.Player.Id && !(request.Player.IsOwner ?? false))
            {
                throw new PermissionException(Permission.Edit);
            }

            Guard.Argument(image.GameId == request.GameId, nameof(request.GameId));

            // Only a managed file is ours to delete — after the DB row is gone, so a failed
            // SaveChanges doesn't orphan a DB row pointing at nothing. A linked resource's
            // real file is never touched here: this codepath serves both "delete" (WS
            // resource_delete, used by MaterialsPanel's delete button) and "unlink" (same
            // button, relabeled client-side for linked resources) — they differ only in
            // whether Storage says there's a file we own.
            var pathToDelete = image.Storage == ResourceStorageKind.ManagedFile ? image.Path : null;

            // Bug fix: mirrors GenericDeleteHandler's cleanup (the base RemoveCard/RemoveAction
            // already go through) — this handler can't use that generic base directly (custom
            // lookup-by-id-or-key, IsOwner permission fallback, physical file deletion), but
            // without these two RemoveRanges every resource deletion orphaned Properties/
            // Permissions rows referencing the now-gone resource id. Addon uninstalls remove many
            // resources at once, so this was the single largest source of that garbage.
            dbContext.RemoveRange(dbContext.Properties.Where(x => x.ParentID == image.Id));
            dbContext.RemoveRange(dbContext.Permissions.Where(x => x.ModelID == image.Id));

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
    }
}


using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Resources.DeleteResourceData
{
    internal class DeleteResourceDataCommandHandler
        : HandlerBase<DeleteResourceDataCommand, CommandResponse>
    {
        private readonly IFileStorageProvider storage;

        public DeleteResourceDataCommandHandler(IDbContext dbContext, IMapper mapper, IFileStorageProvider storage)
            : base(dbContext, mapper)
        {
            this.storage = storage;
        }

        public override async Task<CommandResponse> Handle(
            DeleteResourceDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            DndOnePlaceManager.Domain.Entities.Resources.ResourceModel? resource = null;

            if (!string.IsNullOrWhiteSpace(request.Key))
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Key == request.Key, cancellationToken);
            else if (request.Id.HasValue)
                resource = await dbContext.Resources.FirstOrDefaultAsync(
                    r => r.GameId == request.GameId && r.Id == request.Id.Value, cancellationToken);

            Guard.NotFound(resource, "Resource", (object?)request.Key ?? request.Id);

            var canWrite = resource.PlayerId == request.Player.Id
                || (request.Player.IsOwner ?? false)
                || (request.Player.Permission.HasValue
                    && (request.Player.Permission.Value & Permission.Edit) != 0);

            if (!canWrite)
                throw new PermissionException(Permission.Edit);

            var treeEntries = dbContext.TreeEntries
                .Where(t => t.TargetId == resource.Id).ToList();

            if (treeEntries.Count > 0)
                dbContext.TreeEntries.RemoveRange(treeEntries);

            // Only a managed file is ours to delete — after the DB row is gone (not before, so
            // a failed SaveChanges doesn't leave an orphaned DB row pointing at nothing).
            // A linked resource's real file is never touched: this is "unlink," not "delete."
            var pathToDelete = resource.Storage == ResourceStorageKind.ManagedFile ? resource.Path : null;

            dbContext.Resources.Remove(resource);
            dbContext.SaveChanges();

            if (pathToDelete != null)
                await storage.DeleteAsync(pathToDelete);

            return CommandResponse.Ok;
        }
    }
}

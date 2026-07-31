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
        public DeleteResourceDataCommandHandler(IDbContext dbContext, IMapper mapper)
            : base(dbContext, mapper) { }

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

            dbContext.Resources.Remove(resource);
            dbContext.SaveChanges();
            return CommandResponse.Ok;
        }
    }
}

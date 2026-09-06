using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class ReorderPropertyListCommandHandler : HandlerBase<ReorderPropertyListCommand, (CommandResponse, PropertyDTO)>
    {
        public ReorderPropertyListCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(ReorderPropertyListCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var propertyEntity = await dbContext.Properties.FindAsync(request.PropertyId);
            Guard.NotFound(propertyEntity, "Property", request.PropertyId);

            var type = propertyEntity.EntityName?.ToEntityType();
            Guard.Argument(type != null, nameof(propertyEntity.EntityName));

            var entity = dbContext.Find(type, propertyEntity.ParentID);
            Guard.NotFound(entity, type.Name, propertyEntity.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            var items = PropertyListJson.Deserialize(propertyEntity.Value);
            var byId = items.ToDictionary(i => i.Id);

            // Items are re-ordered by identity, never re-created — ids and fields are
            // untouched. Any id in OrderedItemIds that no longer exists is skipped;
            // any existing item not mentioned is appended, preserving its relative order.
            var ordered = (request.OrderedItemIds ?? new List<string>())
                .Where(byId.ContainsKey)
                .Select(id => byId[id])
                .ToList();

            var orderedIds = new HashSet<string>(ordered.Select(i => i.Id));
            ordered.AddRange(items.Where(i => !orderedIds.Contains(i.Id)));

            propertyEntity.Value = PropertyListJson.Serialize(ordered);

            await dbContext.SaveChangesAsync(cancellationToken);
            return (CommandResponse.Ok, mapper.Map<PropertyDTO>(propertyEntity));
        }
    }
}

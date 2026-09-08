using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class RemovePropertyListItemCommandHandler : HandlerBase<RemovePropertyListItemCommand, (CommandResponse, PropertyDTO)>
    {
        public RemovePropertyListItemCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(RemovePropertyListItemCommand request, CancellationToken cancellationToken)
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
            var item = items.FirstOrDefault(i => i.Id == request.ItemId);
            Guard.NotFound(item, "PropertyListItem", request.ItemId);

            // Keyed removal — no positional shift for the ids of surrounding items,
            // unlike the old name_0..N-1 shift-based scheme this replaces.
            items.Remove(item);
            propertyEntity.Value = PropertyListJson.Serialize(items);

            await dbContext.SaveChangesAsync(cancellationToken);
            return (CommandResponse.Ok, mapper.Map<PropertyDTO>(propertyEntity));
        }
    }
}

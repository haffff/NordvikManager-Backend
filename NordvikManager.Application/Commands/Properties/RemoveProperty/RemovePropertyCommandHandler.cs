using NordvikManager.Application.Mapping;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Guards;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;

namespace NordvikManager.Application.Commands.Properties
{
    public class RemovePropertyCommandHandler : HandlerBase<RemovePropertyCommand, (CommandResponse, PropertyDTO)>
    {
        public RemovePropertyCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(RemovePropertyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Retrieve the property from the database
            var property = dbContext.Properties.Find(request.Id);

            // Check if the property exists
            Guard.NotFound(property, "Property", request.Id);

            var entityType = property?.EntityName?.ToEntityType();
            Guard.Argument(entityType != null, nameof(entityType));

            var entity = dbContext.Find(entityType, property.ParentID);
            Guard.NotFound(entity, entityType.Name, property.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            // Capture the DTO before deletion — the broadcast needs to describe what was removed.
            var dto = mapper.Map<PropertyDTO>(property);

            // Remove the property from the database
            dbContext.Properties.Remove(property);
            dbContext.SaveChanges();

            return (CommandResponse.Ok, dto);
        }
    }
}

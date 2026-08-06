using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Properties.GetProperty
{
    public class GetPropertyCommandHandler : HandlerBase<GetPropertyCommand, (CommandResponse, PropertyDTO)>
    {
        public GetPropertyCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {

        }

        public async override Task<(CommandResponse, PropertyDTO)> Handle(GetPropertyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Example code to get property by Id

            PropertyModel property = null;

            if (request.Id == null && request.ParentID != null && request.Name != null)
            {
                property = dbContext.Properties.FirstOrDefault(p => p.ParentID == request.ParentID && p.Name == request.Name);
            }
            else
            {
                property = dbContext.Properties.FirstOrDefault(p => p.Id == request.Id);
            }

            Guard.NotFound(property, "Property", (object?)request.Id ?? request.Name);

            var entityType = property.EntityName?.ToEntityType();
            Guard.Argument(entityType != null, nameof(property.EntityName));

            var entity = dbContext.Find(entityType, property.ParentID);
            Guard.Argument(entity != null, nameof(entity));

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Read);

            var propertyDto = mapper.Map<PropertyDTO>(property);
            if (property.IsProtected)
                propertyDto.Value = null;

            return (CommandResponse.Ok, propertyDto);
        }
    }
}

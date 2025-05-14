using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
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

            if(request.Id == null && request.ParentID != null && request.Name != null)
            {
                property = dbContext.Properties.FirstOrDefault(p => p.ParentID == request.ParentID && p.Name == request.Name);
            }
            else
            {
                property = dbContext.Properties.FirstOrDefault(p => p.Id == request.Id);
            }

            if (property == null)
            {
                return (CommandResponse.NoResource, null);
            }

            var entityType = property?.EntityName?.ToEntityType();
            if (entityType != null)
            {
                var entity = dbContext.Find(entityType, property.ParentID);

                if (entity == null)
                {
                    return (CommandResponse.WrongArguments, null);
                }

                if (!(entity as IEntity).HasPermission(request.Player?.Id ?? default, Permission.Read))
                {
                    return (CommandResponse.NoPermission, null);
                }

                var propertyDto = mapper.Map<PropertyDTO>(property);

                return (CommandResponse.Ok, propertyDto);
            }
            return (CommandResponse.WrongArguments, null);
        }
    }
}

using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    public class RemovePropertyCommandHandler : HandlerBase<RemovePropertyCommand, CommandResponse>
    {
        public RemovePropertyCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(RemovePropertyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            // Retrieve the property from the database
            var property = dbContext.Properties.Find(request.Id);

            // Check if the property exists
            if (property == null)
            {
                throw new ResourceNotFoundException(nameof(property));
            }

            var entityType = property?.EntityName?.ToEntityType();
            if (entityType != null)
            {
                var entity = dbContext.Find(entityType, property.ParentID);

                if (entity == null)
                {
                    throw new ResourceNotFoundException(nameof(entity));
                }

                (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

                // Remove the property from the database
                dbContext.Properties.Remove(property);
                dbContext.SaveChanges();

                return CommandResponse.Ok;
            }

            throw new WrongArgumentsException(nameof(entityType));
        }
    }
}

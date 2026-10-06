using AutoMapper;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Properties.AddProperty
{
    public class AddPropertyCommandHandler : HandlerBase<AddPropertyCommand, (CommandResponse, PropertyDTO)>
    {
        public AddPropertyCommandHandler(IDbContext battleMapContext, IMapper mapper) : base(battleMapContext, mapper)
        {
        }

        public async override Task<(CommandResponse, PropertyDTO)> Handle(AddPropertyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var type = await dbContext.DetectEntityTypeAsync((Guid)request.Property.ParentID);
            Guard.NotFound(type, "Entity", request.Property.ParentID);

            var entity = dbContext.Find(type, request.Property.ParentID);
            Guard.NotFound(entity, type.Name, request.Property.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            // Map PropertyDTO to domain entity
            var property = mapper.Map<PropertyModel>(request.Property);
            property.ParentID = (Guid)request.Property.ParentID;
            property.EntityName = type.Name;

            var propId = await PropertyExists(property);
            if (propId != null)
            {
                return (CommandResponse.AlreadyExists, null);
            }

            AddProperty(request, entity, property);

            // Add the property to the database
            var result = await dbContext.SaveChangesAsync(cancellationToken);

            return (CommandResponse.Ok, mapper.Map<PropertyDTO>(property));
        }

        private async Task<Guid?> PropertyExists(PropertyModel property)
        {
            return (await dbContext.Properties.FirstOrDefaultAsync(p => p.Name == property.Name && p.ParentID == property.ParentID))?.Id;
        }

        // Attach to the owner (already loaded above) and add the row; loading the owner's
        // properties collection just to append to it read every existing property.
        private void AddProperty(AddPropertyCommand request, object entity, PropertyModel property)
        {
            switch (entity)
            {
                case GameModel game:
                    property.Game = game;
                    break;
                case MapModel map:
                    property.Map = map;
                    break;
                case ElementModel element:
                    property.Element = element;
                    break;
                case CardModel card:
                    property.Card = card;
                    break;
                default:
                    return;
            }

            dbContext.Properties.Add(property);
        }
    }
}

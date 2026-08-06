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

        private void AddProperty(AddPropertyCommand request, object entity, PropertyModel property)
        {
            switch (entity)
            {
                case GameModel:
                    dbContext.Games.Include(g => g.Properties).FirstOrDefault(g => g.Id == request.Property.ParentID).Properties.Add(property);
                    break;
                case MapModel:
                    dbContext.Maps.Include(m => m.Properties).FirstOrDefault(m => m.Id == request.Property.ParentID).Properties.Add(property);
                    break;
                case ElementModel:
                    dbContext.Elements.Include(e => e.Properties).FirstOrDefault(e => e.Id == request.Property.ParentID).Properties.Add(property);
                    break;
                case CardModel:
                    dbContext.Cards.Include(c => c.Properties).FirstOrDefault(c => c.Id == request.Property.ParentID).Properties.Add(property);
                    break;
                default:
                    break;
            }
        }
    }
}

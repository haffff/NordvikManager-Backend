using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace DndOnePlaceManager.Application.Commands.Properties.AddProperties
{
    internal class AddPropertiesCommandHandler : HandlerBase<AddPropertiesCommand, CommandResponse>
    {
        public AddPropertiesCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<CommandResponse> Handle(AddPropertiesCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!request.Properties.Any())
            {
                return CommandResponse.NoChange;
            }

            var firstProperty = request.Properties.FirstOrDefault();
            Guard.Argument(request.Properties.All(x => x.ParentID == firstProperty.ParentID), nameof(request.Properties));

            var type = await dbContext.DetectEntityTypeAsync((Guid)firstProperty.ParentID);
            Guard.NotFound(type, "Entity", firstProperty.ParentID);

            var entity = dbContext.Find(type, firstProperty.ParentID);

            Guard.NotFound(entity, type.Name, firstProperty.ParentID);

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            var properties = request.Properties.Select(x => mapper.Map<PropertyModel>(x)).ToArray();
            foreach (var property in properties) property.EntityName = type.Name;

            if (AddProperties(request, entity, properties))
            {
                dbContext.SaveChanges();
                return CommandResponse.Ok;
            }

            throw new ResourceNotFoundException(type.Name, firstProperty.ParentID);
        }

        private bool AddProperties(AddPropertiesCommand request, object entity, PropertyModel[] property)
        {
            Guid parentId = request.Properties.First().ParentID ?? default;

            switch (entity)
            {
                case GameModel game:
                    foreach (var item in property) item.Game = game;
                    break;
                case MapModel map:
                    foreach (var item in property) item.Map = map;
                    break;
                case ElementModel element:
                    foreach (var item in property) item.Element = element;
                    break;
                case CardModel card:
                    foreach (var item in property) item.Card = card;
                    break;
                default:
                    return false;
            }

            // Skip names the owner already has — checked by name, without loading its properties.
            var names = property.Select(x => x.Name).ToList();
            var existing = dbContext.Properties
                .Where(x => x.ParentID == parentId && names.Contains(x.Name))
                .Select(x => x.Name)
                .ToHashSet();

            dbContext.Properties.AddRange(property.Where(x => !(x.ParentID == parentId && existing.Contains(x.Name))));

            return true;
        }
    }
}

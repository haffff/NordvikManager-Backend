using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
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

            if(!request.Properties.Any())
            {
                return CommandResponse.NoChange;
            }

            var firstProperty = request.Properties.FirstOrDefault();
            var type = firstProperty.EntityName?.ToEntityType();

            if (!request.Properties.All(x => x.ParentID == firstProperty.ParentID) && type != null)
            {
                throw new WrongArgumentsException(nameof(request.Properties));
            }

            var entity = dbContext.Find(type, firstProperty.ParentID);

            if (entity == null)
            {
                throw new ResourceNotFoundException(nameof(entity));
            }

            (entity as IEntity).ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

            if (AddProperties(request, entity, request.Properties.Select(x => mapper.Map<PropertyModel>(x)).ToArray()))
            {
                dbContext.SaveChanges();
                return CommandResponse.Ok;
            }

            throw new ResourceNotFoundException(nameof(entity));
        }

        private bool AddProperties(AddPropertiesCommand request, object entity, PropertyModel[] property)
        {
            List<PropertyModel> properties = null;
            Guid parentId = request.Properties.First().ParentID ?? default;

            switch (entity)
            {
                case GameModel game:
                    properties = dbContext.Games.Where(g => g.Id == parentId).Include(g => g.Properties).FirstOrDefault(x => x.Id == parentId)?.Properties;
                    foreach (var item in property)
                    {
                        item.Game = game;
                    }
                    break;
                case MapModel map:
                    properties = dbContext.Maps.Where(g => g.Id == parentId).Include(g => g.Properties).FirstOrDefault(x => x.Id == parentId)?.Properties;
                    foreach (var item in property)
                    {
                        item.Map = map;
                    }
                    break;
                case ElementModel element:
                    properties = dbContext.Elements.Where(g => g.Id == parentId).Include(g => g.Properties).FirstOrDefault(x => x.Id == parentId)?.Properties;
                    foreach (var item in property)
                    {
                        item.Element = element;
                    }
                    break;
                case CardModel card:
                    properties = dbContext.Cards.Where(g => g.Id == parentId).Include(g => g.Properties).FirstOrDefault(x => x.Id == parentId)?.Properties;
                    foreach (var item in property)
                    {
                        item.Card = card;
                    }
                    break;
                default:
                    break;
            }

            if (properties == null)
            {
                return false;
            }

            var hashSet = properties.Select(x => new { name = x.Name, parent = x.ParentID }).ToHashSet();
            var newProperties = property.Where(x => !hashSet.Contains(new { name = x.Name, parent = x.ParentID }));

            properties.AddRange(newProperties);

            return true;
        }
    }
}

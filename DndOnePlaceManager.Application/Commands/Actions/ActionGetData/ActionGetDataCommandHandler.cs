using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;

namespace DndOnePlaceManager.Application.Commands.Actions.ActionGetData
{
    internal class ActionGetDataCommandHandler : HandlerBase<ActionGetDataCommand, List<IGameDataTransferObject>>
    {
        public ActionGetDataCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public async override Task<List<IGameDataTransferObject>> Handle(ActionGetDataCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var type = request.EntityType.ToEntityType();

            if (request.ID != null)
            {
                var model = dbContext.Find(type, request.ID);
                var destinationType = type.GetDTOType();
                var dto = mapper.Map(model, type, destinationType);
                return new List<IGameDataTransferObject> { dto as IGameDataTransferObject };
            }

            if (!String.IsNullOrEmpty(request.Property))
            {
                return FindByProperty(request, type);
            }

            var game = dbContext.Games
                .Include(x=>x.Maps).ThenInclude(x=>x.Elements)
                .Include(x=>x.Actions)
                .Include(x=>x.BattleMaps)
                .Include(x=>x.Cards)
                .Include(x=>x.Layouts)
                .Include(x=>x.Properties)
                .FirstOrDefault(x => x.Id == request.GameID);

            if (request.Name != null)
            {
                return FindByName(request.Name, type, game);
            }

            switch (request.EntityType)
            {
                case "MapModel":
                    return game.Maps.ToList().Select(x => mapper.Map<MapDTO>(x)).Cast<IGameDataTransferObject>().ToList();
                case "CardModel":
                    return game.Cards.ToList().Select(x => mapper.Map<CardDto>(x)).Cast<IGameDataTransferObject>().ToList();
                case "LayoutModel":
                    return game.Layouts.ToList().Select(x => mapper.Map<LayoutDTO>(x)).Cast<IGameDataTransferObject>().ToList();
                case "ActionModel":
                    return game.Actions.ToList().Select(x => mapper.Map<ActionDto>(x)).Cast<IGameDataTransferObject>().ToList();
                case "ElementModel":
                    return game.Maps.SelectMany(x=>x.Elements).ToList().Select(x => mapper.Map<ElementDTO>(x)).Cast<IGameDataTransferObject>().ToList();
                default:
                    break;
            }

            return new List<IGameDataTransferObject>();
        }

        private List<IGameDataTransferObject> FindByProperty(ActionGetDataCommand request, Type? type)
        {
            var list = new List<IGameDataTransferObject>();

            var entities = dbContext.GetEntitiesList(request.GameID, request.EntityType);

            foreach (var property in dbContext.Properties.Where(x=>x.EntityName == request.EntityType && x.Name == request.Property))
            {
                var entity = entities.FirstOrDefault(x => x.Id == property.ParentID);
                if (entity != null)
                {
                    list.Add(MapToDTO(entity));
                }
            }

            return list;
        }

        private IGameDataTransferObject MapToDTO(IEntity entity)
        {
            var type = entity.GetType();
            var dtoType = type.GetDTOType();
            return mapper.Map(entity, type, dtoType) as IGameDataTransferObject;
        }

        private List<IGameDataTransferObject> FindByName(string name, Type? type, GameModel game)
        {
            var dtoType = type.GetDTOType();

            switch (type.Name)
            {
                case "MapModel":
                    return game.Maps.Where(x => x.Name == name).ToList().Select(x=>mapper.Map<MapDTO>(x)).Cast<IGameDataTransferObject>().ToList();
                case "CardModel":
                    return game.Cards.Where(x => x.Name == name).ToList().Select(x => mapper.Map<CardDto>(x)).Cast<IGameDataTransferObject>().ToList();
                case "LayoutModel":
                    return game.Layouts.Where(x => x.Name == name).ToList().Select(x => mapper.Map<LayoutDTO>(x)).Cast<IGameDataTransferObject>().ToList();
                case "ActionModel":
                    return game.Actions.Where(x => x.Name == name).ToList().Select(x => mapper.Map<ActionDto>(x)).Cast<IGameDataTransferObject>().ToList();
                default:
                    break;
            }

            return new List<IGameDataTransferObject>();
        }
    }
}


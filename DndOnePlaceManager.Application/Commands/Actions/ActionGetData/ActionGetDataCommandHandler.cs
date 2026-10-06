using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using ActionModel = DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;
using Microsoft.EntityFrameworkCore;

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

            // ToEntityType doesn't know every type (e.g. ActionModel); fall back to the name as given.
            var typeName = type?.Name ?? request.EntityType;

            // By id too, only within this game.
            if (request.ID != null)
                return Find(typeName, request.GameID, name: null, new List<Guid> { request.ID.Value });

            if (!String.IsNullOrEmpty(request.Property))
            {
                var ownerIds = dbContext.Properties
                    .Where(x => x.EntityName == request.EntityType && x.Name == request.Property)
                    .Select(x => x.ParentID)
                    .ToList();
                return Find(typeName, request.GameID, name: null, ownerIds);
            }

            return Find(typeName, request.GameID, request.Name, ids: null);
        }

        // Loads only the requested type, of this game, with the name/id filters applied in SQL.
        // Maps come with their elements, as action scripts have always received them.
        private List<IGameDataTransferObject> Find(string? entityType, Guid gameId, string? name, List<Guid>? ids)
        {
            switch (entityType)
            {
                case "MapModel":
                    return Query<MapModel, MapDTO>(dbContext.Maps.Include(x => x.Elements).Where(x => x.Game.Id == gameId), name, ids);
                case "CardModel":
                    return Query<CardModel, CardDto>(dbContext.Cards.Where(x => x.GameId == gameId), name, ids);
                case "LayoutModel":
                    return Query<LayoutModel, LayoutDTO>(dbContext.Layouts.Where(x => x.GameModelId == gameId), name, ids);
                case "ActionModel":
                    return Query<ActionModel, ActionDto>(dbContext.Actions.Where(x => x.Game.Id == gameId), name, ids);
                case "PropertyModel":
                    return Query<PropertyModel, PropertyDTO>(dbContext.Properties.InGame(gameId), name, ids);
                case "ElementModel":
                    if (name != null)
                        return new List<IGameDataTransferObject>(); // elements have no name
                    var elements = dbContext.Elements.Where(x => x.Map!.Game.Id == gameId);
                    if (ids != null)
                        elements = elements.Where(x => ids.Contains(x.Id));
                    return elements.AsEnumerable().Select(x => (IGameDataTransferObject)mapper.Map<ElementDTO>(x)).ToList();
                default:
                    return new List<IGameDataTransferObject>();
            }
        }

        private List<IGameDataTransferObject> Query<TModel, TDto>(IQueryable<TModel> query, string? name, List<Guid>? ids)
            where TModel : class, INamedEntity
            where TDto : IGameDataTransferObject
        {
            if (name != null)
                query = query.Where(x => x.Name == name);
            if (ids != null)
                query = query.Where(x => ids.Contains(x.Id));
            return query.AsEnumerable().Select(x => (IGameDataTransferObject)mapper.Map<TDto>(x)!).ToList();
        }
    }
}

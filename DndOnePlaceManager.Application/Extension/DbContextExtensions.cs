using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Extension
{
    public static class DbContextExtensions
    {
        /// <summary>
        /// Resolves the actual entity type an ID belongs to by checking Elements, Maps,
        /// Cards, and Games directly — rather than trusting a client-supplied EntityName,
        /// which a caller can get wrong (e.g. tagging a GameModel-owned property as
        /// "CardModel"). GUIDs are unique across all entity tables, so no game-scoping
        /// is needed here; callers still enforce authorization via ThrowIfNoPermission.
        /// </summary>
        public static async Task<Type?> DetectEntityTypeAsync(this IDbContext context, Guid id)
        {
            if (await context.Elements.AnyAsync(e => e.Id == id)) return typeof(ElementModel);
            if (await context.Maps.AnyAsync(m => m.Id == id)) return typeof(MapModel);
            if (await context.Cards.AnyAsync(c => c.Id == id)) return typeof(CardModel);
            if (await context.Games.AnyAsync(g => g.Id == id)) return typeof(GameModel);
            return null;
        }

        public static List<IEntity> GetEntitiesList(this IDbContext context, Guid gameId, string entityType)
        {
            switch (entityType.ToLower())
            {
                case "mapmodel":
                    return context.Games.Find(gameId)?.Maps?.Select(x => x as IEntity).ToList();
                case "elementmodel":
                    return context.Games.Include(x => x.Maps).ThenInclude(x => x.Elements).FirstOrDefault(x => x.Id == gameId).Maps.SelectMany(x => x.Elements).Cast<IEntity>().ToList();
                case "propertymodel":
                    return context.Games.Find(gameId)?.Properties?.Select(x => x as IEntity).ToList();
                case "layoutmodel":
                    return context.Games.Find(gameId)?.Layouts?.Select(x => x as IEntity).ToList();
                case "cardmodel":
                    return context.Games.Find(gameId)?.Cards?.Select(x => x as IEntity).ToList();
                case "battlemapmodel":
                    return context.Games.Find(gameId)?.BattleMaps?.Select(x => x as IEntity).ToList();
                default:
                    return new List<IEntity>();
            }
        }
    }
}
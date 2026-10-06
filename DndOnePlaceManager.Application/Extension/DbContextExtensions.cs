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
    }
}
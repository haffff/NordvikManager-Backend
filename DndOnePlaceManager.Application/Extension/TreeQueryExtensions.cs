using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Extension
{
    public static class TreeQueryExtensions
    {
        /// <summary>
        /// Loads one tree of the game (the entries of one EntryType, with Parent and Next) instead
        /// of every tree entry of every type — resource trees alone grow to thousands of entries.
        /// </summary>
        public static IQueryable<GameModel> IncludeTree(this IQueryable<GameModel> games, string? entryType) =>
            games
                .Include(g => g.TreeEntries.Where(t => t.EntryType == entryType)).ThenInclude(t => t.Parent)
                .Include(g => g.TreeEntries.Where(t => t.EntryType == entryType)).ThenInclude(t => t.Next);
    }
}

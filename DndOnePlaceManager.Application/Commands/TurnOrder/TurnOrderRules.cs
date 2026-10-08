using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TurnOrder
{
    /// <summary>How turns pass, shared by the turn order command and token removal.</summary>
    internal static class TurnOrderRules
    {
        public static List<TurnOrderEntryModel> Ordered(TurnOrderModel order) =>
            order.Entries.OrderBy(e => e.Position).ToList();

        /// <summary>Positions 0..n-1 in the given order.</summary>
        public static void Renumber(IReadOnlyList<TurnOrderEntryModel> ordered)
        {
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].Position = i;
        }

        /// <summary>
        /// Steps the turn: past the last entry it wraps and starts the next round; back past
        /// the first it goes to the previous round, but never below round 1. Hidden entries
        /// are not skipped (the GM runs their turns).
        /// </summary>
        public static void Step(TurnOrderModel order, int direction)
        {
            var ordered = Ordered(order);
            if (ordered.Count == 0)
            {
                order.CurrentEntryId = null;
                return;
            }

            var index = ordered.FindIndex(e => e.Id == order.CurrentEntryId);
            if (index < 0)
            {
                order.CurrentEntryId = ordered[0].Id;
                return;
            }

            var next = index + (direction < 0 ? -1 : 1);
            if (next >= ordered.Count)
            {
                next = 0;
                order.Round++;
            }
            else if (next < 0)
            {
                if (order.Round > 1)
                {
                    order.Round--;
                    next = ordered.Count - 1;
                }
                else
                {
                    next = 0;
                }
            }
            order.CurrentEntryId = ordered[next].Id;
        }

        /// <summary>
        /// Removes entries; if the current one goes, the turn passes to the entry after it
        /// (wrapping into the next round past the end).
        /// </summary>
        public static void Remove(TurnOrderModel order, ICollection<TurnOrderEntryModel> removed, IDbContext dbContext)
        {
            if (removed.Count == 0)
                return;

            var ordered = Ordered(order);
            var current = ordered.FirstOrDefault(e => e.Id == order.CurrentEntryId);
            if (current != null && removed.Contains(current))
            {
                var index = ordered.IndexOf(current);
                var after = ordered.Skip(index + 1).FirstOrDefault(e => !removed.Contains(e));
                var wrapped = after == null ? ordered.FirstOrDefault(e => !removed.Contains(e)) : null;
                order.CurrentEntryId = (after ?? wrapped)?.Id;
                if (wrapped != null)
                    order.Round++;
            }

            foreach (var entry in removed)
            {
                order.Entries.Remove(entry);
                dbContext.TurnOrderEntries.Remove(entry);
            }
            Renumber(Ordered(order));
            if (order.Entries.Count == 0)
                order.CurrentEntryId = null;
        }
    }

    /// <summary>Keeps turn orders in step when a token leaves its map.</summary>
    public static class TurnOrderCleanup
    {
        /// <summary>
        /// Takes a removed token out of any turn order. Returns the map whose turn order
        /// changed (so players can be told), or null.
        /// </summary>
        public static async Task<Guid?> RemoveElementAsync(IDbContext dbContext, Guid elementId)
        {
            var order = await dbContext.TurnOrders
                .Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.Entries.Any(e => e.ElementId == elementId));
            if (order == null)
                return null;

            TurnOrderRules.Remove(order, order.Entries.Where(e => e.ElementId == elementId).ToList(), dbContext);
            await dbContext.SaveChangesAsync();
            return order.MapId;
        }
    }
}

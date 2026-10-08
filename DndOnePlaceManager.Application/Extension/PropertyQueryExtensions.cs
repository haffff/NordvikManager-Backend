using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Extension
{
    public static class PropertyQueryExtensions
    {
        /// <summary>
        /// Properties owned by the game itself or by one of its maps, elements or cards.
        /// </summary>
        public static IQueryable<PropertyModel> InGame(this IQueryable<PropertyModel> query, Guid gameId) =>
            query.Where(p =>
                p.Game!.Id == gameId ||
                p.Map!.Game.Id == gameId ||
                p.Element!.Map!.Game.Id == gameId ||
                p.Card!.GameId == gameId);
    }
}

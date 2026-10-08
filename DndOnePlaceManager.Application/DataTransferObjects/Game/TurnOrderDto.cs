namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    /// <summary>A map's turn order as one player may see it.</summary>
    public class TurnOrderDto
    {
        public Guid MapId { get; set; }
        public int Round { get; set; } = 1;

        /// <summary>Whose turn it is; null when the order is empty or (for players) the entry is hidden.</summary>
        public Guid? CurrentEntryId { get; set; }

        /// <summary>For players: it's the turn of an entry they can't see.</summary>
        public bool CurrentHidden { get; set; }

        /// <summary>This player may end the current turn: the GM, or whoever controls the current token.</summary>
        public bool CanEndTurn { get; set; }

        /// <summary>This player may change the turn order (Edit on the map).</summary>
        public bool CanEdit { get; set; }

        public List<TurnOrderEntryDto> Entries { get; set; } = new();
    }

    public class TurnOrderEntryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double? Initiative { get; set; }

        /// <summary>The token on the map; null for a free entry.</summary>
        public Guid? ElementId { get; set; }

        public bool Hidden { get; set; }
    }
}

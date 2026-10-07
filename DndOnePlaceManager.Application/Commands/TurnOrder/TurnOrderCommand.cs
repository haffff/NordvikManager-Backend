using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.TurnOrder
{
    public enum TurnOrderOperation
    {
        Add,
        Update,
        Remove,
        Reorder,
        Sort,
        Advance,
        EndTurn,
        Reset,
    }

    /// <summary>
    /// Changes a map's turn order. Everything needs Edit on the map, except EndTurn,
    /// which the player controlling the current entry's token may also do.
    /// </summary>
    public class TurnOrderCommand : CommandBase<(CommandResponse, TurnOrderNotice?)>
    {
        public Guid GameId { get; set; }
        public Guid MapId { get; set; }
        public PlayerDTO Player { get; set; } = null!;
        public TurnOrderOperation Operation { get; set; }

        /// <summary>Add: tokens (ElementId) and/or free entries (Name).</summary>
        public List<TurnOrderEntryInput>? Entries { get; set; }

        /// <summary>Update: the entry; Advance: jump to this entry instead of stepping.</summary>
        public Guid? EntryId { get; set; }

        /// <summary>Update: new values (null = unchanged; ClearInitiative removes it).</summary>
        public string? Name { get; set; }
        public double? Initiative { get; set; }
        public bool ClearInitiative { get; set; }
        public bool? Hidden { get; set; }

        /// <summary>Update: sort by initiative afterwards.</summary>
        public bool SortAfter { get; set; }

        /// <summary>Remove: these entries. Reorder: all entries, in their new order.</summary>
        public List<Guid>? EntryIds { get; set; }

        /// <summary>Remove: the entries of these tokens.</summary>
        public List<Guid>? ElementIds { get; set; }

        /// <summary>Advance: +1 next turn, -1 previous.</summary>
        public int Direction { get; set; } = 1;

        /// <summary>Reset: remove all entries instead of restarting at round 1.</summary>
        public bool Clear { get; set; }
    }

    public class TurnOrderEntryInput
    {
        public Guid? ElementId { get; set; }
        public string? Name { get; set; }
        public double? Initiative { get; set; }
        public bool? Hidden { get; set; }
    }

    /// <summary>
    /// What changed, safe for everyone: sent to all players (who then fetch what they
    /// may see) and to the Turn Changed hook. Says nothing about a hidden entry.
    /// </summary>
    public class TurnOrderNotice
    {
        public Guid MapId { get; set; }
        public int Round { get; set; }

        /// <summary>Whose turn it is; null when empty or when that entry is hidden.</summary>
        public Guid? CurrentEntryId { get; set; }

        /// <summary>The current entry's token, when it's a visible token.</summary>
        public Guid? ElementId { get; set; }

        /// <summary>The turn passed to another entry, or the round changed.</summary>
        public bool TurnChanged { get; set; }

        /// <summary>Add: the new entries' ids (for action steps).</summary>
        public List<Guid> AddedEntryIds { get; set; } = new();
    }
}

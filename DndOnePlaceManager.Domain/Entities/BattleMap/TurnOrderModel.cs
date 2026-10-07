using DndOnePlaceManager.Domain.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DndOnePlaceManager.Domain.Entities.BattleMap
{
    /// <summary>
    /// A map's turn order (initiative tracker): its entries in order, the round, and
    /// whose turn it is. One per map.
    /// </summary>
    public class TurnOrderModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid MapId { get; set; }
        public DNDOnePlaceManager.Domain.Entities.BattleMap.MapModel? Map { get; set; }

        public int Round { get; set; } = 1;

        /// <summary>The entry whose turn it is; null when the order is empty.</summary>
        public Guid? CurrentEntryId { get; set; }

        public List<TurnOrderEntryModel> Entries { get; set; } = new();
    }

    /// <summary>One place in a turn order: a token on the map, or a free entry (name only).</summary>
    public class TurnOrderEntryModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public Guid TurnOrderId { get; set; }
        public TurnOrderModel? TurnOrder { get; set; }

        /// <summary>Place in the order, 0 first.</summary>
        public int Position { get; set; }

        public string Name { get; set; } = string.Empty;

        public double? Initiative { get; set; }

        /// <summary>The token (element on the same map) this entry is for; null for a free entry.</summary>
        public Guid? ElementId { get; set; }

        /// <summary>Not shown to players (the GM still runs its turn).</summary>
        public bool Hidden { get; set; }
    }
}

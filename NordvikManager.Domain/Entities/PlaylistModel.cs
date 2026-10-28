using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordvikManager.Domain.Entities
{
    public class PlaylistModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        [ForeignKey("Game")]
        public Guid GameId { get; set; }
        public GameModel Game { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public PlaybackMode Mode { get; set; } = PlaybackMode.Sequential;
        public bool Shuffle { get; set; } = false;
        public bool Repeat { get; set; } = true;
        public PlaylistKind Kind { get; set; } = PlaylistKind.Music;

        /// <summary>How loud the GM wants it, 0..1 (each player's own volume applies on top).</summary>
        public double Volume { get; set; } = 1;

        /// <summary>Soundboard: its sounds that repeat until stopped (rain, a fire).</summary>
        public List<Guid> LoopedResourceIds { get; set; } = new();

        public List<ResourceModel> Resources { get; set; } = new();
    }
}

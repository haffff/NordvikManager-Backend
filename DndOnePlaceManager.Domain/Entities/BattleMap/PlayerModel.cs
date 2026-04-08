using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Resources;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDOnePlaceManager.Domain.Entities.BattleMap
{
    public class PlayerModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public bool System { get; set; }
        /// <summary>Local username reference. Kept for backward compatibility.</summary>
        public string? User { get; set; }
        /// <summary>User UUID from the Central Server (JWT sub claim).</summary>
        public string? CentralServerUserId { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? Image { get; set; }
        public List<ResourceModel> Resources { get; set; }
        public GameModel Game { get; set; }
    }
}

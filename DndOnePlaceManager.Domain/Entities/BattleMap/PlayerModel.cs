using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Resources;
using DNDOnePlaceManager.Domain.Entities.Auth;
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
        public string? User { get; set; }
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? Image { get; set; }
        public List<ResourceModel> Resources { get; set; }
        public GameModel Game { get; set; }
    }
}

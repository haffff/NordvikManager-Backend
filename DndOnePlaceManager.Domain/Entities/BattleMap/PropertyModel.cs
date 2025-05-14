using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDOnePlaceManager.Domain.Entities.BattleMap
{
    public class PropertyModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? EntityName { get; set; }
        public string? Value { get; set; }
        public Guid ParentID { get; set; }
        public GameModel? Game { get; set; }
        public MapModel? Map { get; set; }
        public ElementModel? Element { get; set; }
        public CardModel? Card { get; set; }
    }
}
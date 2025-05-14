using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDOnePlaceManager.Domain.Entities.BattleMap
{
    public class ElementModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public MapModel? Map { get; set; }
        public List<PropertyModel>? Properties { get; set; }
        public List<ElementDetailModel>? Details { get; set; }
        public bool? Selectable { get; set; }
        public bool? IsPublic { get; set; }
        public int Layer { get; set; }
        public Guid MapId { get; set; }
    }
}

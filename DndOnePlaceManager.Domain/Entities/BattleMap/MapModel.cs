using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DNDOnePlaceManager.Domain.Entities.BattleMap
{
    public class MapModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public List<ElementModel>? Elements { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public int GridSize { get; set; }
        public bool GridVisible { get; set; }
        public int GridUnitSize { get; set; }
        public List<PropertyModel>? Properties { get; set; }
        public GameModel Game { get; set; }
    }
}

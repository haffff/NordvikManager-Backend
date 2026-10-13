using DndOnePlaceManager.Domain.Entities.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DndOnePlaceManager.Domain.Entities.BattleMap
{
    public class CardModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public Guid? MainResource { get; set; }
        public string? AdditionalResources { get; set; }
        public bool IsCustomUi { get; set; }
        public bool IsTemplate { get; set; }
        // Left out of template lists (still usable by id/name), e.g. an addon's internal
        // template that only an importer clones. Not inherited by cards made from it.
        public bool IsHidden { get; set; }
        public bool IsR20Card { get; set; }
        public bool? FirstOpen { get; set; }
        public string? Key { get; set; }
        public Guid GameId { get; set; }
        public List<PropertyModel> Properties { get; set; }
        public GameModel Game { get; set; }
    }
}

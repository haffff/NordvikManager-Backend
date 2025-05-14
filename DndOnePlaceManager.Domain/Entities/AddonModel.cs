using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Resources;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Domain.Entities
{
    public class AddonModel : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Key { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public string? ReleaseUrl { get; set; }
        public string? RepositoryUrl { get; set; }
        public string? License { get; set; }
        public List<AddonModel>? Dependencies { get; set; }
        public List<CardModel>? Views { get; set; }
        public List<CardModel>? Templates { get; set; }
        public List<ActionModel>? Actions { get; set; }
        public List<ResourceModel>? Resources { get; set; }
    }
}

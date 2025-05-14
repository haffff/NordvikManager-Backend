using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Resources;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DNDOnePlaceManager.Domain.Entities.BattleMap
{
    public class GameModel : INamedEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public Guid MasterId { get; set; }
        public Guid SystemPlayerId { get; set; }
        public List<MapModel> Maps { get; set; }
        public List<PlayerModel> Players { get; set; }
        public string? Name { get; set; }
        [JsonIgnore]
        public string? Password { get; set; }
        public bool? Visible { get; set; }
        public List<LayoutModel> Layouts { get; set; }
        public List<BattleMapModel> BattleMaps { get; set; }
        public bool UseMetric { get; set; }
        public List<PropertyModel>? Properties { get; set; }
        public List<CardModel> Cards { get; set; }
        public List<ActionModel> Actions { get; set; }
        public List<AddonModel> Addons { get; set; }
        public List<ResourceModel> Resources { get; set; }
        public List<TreeEntryModel> TreeEntries { get; set; }
    }
}

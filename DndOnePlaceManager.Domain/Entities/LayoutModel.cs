using DndOnePlaceManager.Domain.Entities.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Domain.Entities
{
    public class LayoutModel : INamedEntity
    {
        public Guid Id { get; set; }
        public Guid GameModelId { get; set; }
        public GameModel Game { get; set; }
        public string Value { get; set; }
        public string Name { get; set; }
        public bool Default { get; set; }
    }
}

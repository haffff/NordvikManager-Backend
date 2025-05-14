using DndOnePlaceManager.Domain.Entities.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Domain.Entities
{
    public class TreeEntryModel : INamedEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public TreeEntryModel? Parent { get; set; }
        public TreeEntryModel? Next { get; set; }
        public bool IsFolder { get; set; }
        public bool? Head { get; set; }
        public Guid? TargetId { get; set; }
        public string EntryType { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool? NewItem { get; set; }
        public GameModel Game { get; set; }
    }
}

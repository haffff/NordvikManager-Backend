using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class PropertyDTO : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string? Name { get; set; }
        public Guid? ParentID { get; set; }
        public string? EntityName { get; set; }
        public string? Value { get; set; }
        public Permission? Permission { get; set; }
    }
}

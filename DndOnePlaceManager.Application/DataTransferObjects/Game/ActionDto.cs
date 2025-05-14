using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Enums;

namespace DndOnePlaceManager.Application.DataTransferObjects.Game
{
    public class ActionDto : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public string Name { get; set; }
        public string? Path { get; set; }
        public string Description { get; set; }
        public Hook Hook { get; set; }
        public string Prefix { get; set; }
        public bool IsEnabled { get; set; }
        public string Content { get; set; }
        public Permission? Permission { get; set; }
    }
}

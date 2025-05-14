using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.DataTransferObjects.Chat
{
    public class MessageDTO : IGameDataTransferObject
    {
        public Guid? Id { get; set; }
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public string? Data { get; set; }
        public DateTime? Created { get; set; }
        public Permission? Permission { get; set; }
    }
}

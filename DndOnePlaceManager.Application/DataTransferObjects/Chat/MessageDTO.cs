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
        /// <summary>Set on a whisper or GM roll: who it was sent to.</summary>
        public Guid? RecipientId { get; set; }
        /// <summary>Data is a server-made chat template; otherwise it's plain text.</summary>
        public bool IsTemplate { get; set; }
        public Permission? Permission { get; set; }
    }
}

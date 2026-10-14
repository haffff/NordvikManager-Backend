using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Chat.AddMessage
{
    public class AddMessageCommand : CommandBase<(CommandResponse, long)>
    {
        public Guid PlayerId { get; set; }
        public string Message { get; set; }
        public Guid GameID { get; set; }
        /// <summary>Whisper / GM roll recipient.</summary>
        public Guid? RecipientId { get; set; }
        /// <summary>Players who may read it; null = everyone.</summary>
        public List<Guid>? VisibleTo { get; set; }
    }
}

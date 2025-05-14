using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Chat.AddMessage
{
    public class AddMessageCommand : CommandBase<(CommandResponse, long)>
    {
        public Guid PlayerId { get; set; }
        public string Message { get; set; }
        public Guid GameID { get; set; }
    }
}

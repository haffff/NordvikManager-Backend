using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Player.UnbanUser
{
    public class UnbanUserCommand : CommandBase<CommandResponse>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

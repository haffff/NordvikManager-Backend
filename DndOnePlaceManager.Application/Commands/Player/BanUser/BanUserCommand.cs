using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Player.BanUser
{
    public class BanUserCommand : CommandBase<CommandResponse>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

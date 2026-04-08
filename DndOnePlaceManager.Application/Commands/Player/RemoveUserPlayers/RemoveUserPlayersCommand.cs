using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Player.RemoveUserPlayers
{
    public class RemoveUserPlayersCommand : CommandBase<CommandResponse>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

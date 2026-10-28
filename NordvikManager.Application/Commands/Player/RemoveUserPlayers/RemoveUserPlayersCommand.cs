using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Player.RemoveUserPlayers
{
    public class RemoveUserPlayersCommand : CommandBase<CommandResponse>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

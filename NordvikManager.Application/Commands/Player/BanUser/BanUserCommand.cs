using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Player.BanUser
{
    public class BanUserCommand : CommandBase<CommandResponse>
    {
        public string CentralUserId { get; set; } = string.Empty;
    }
}

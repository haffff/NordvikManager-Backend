using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Player.KickPlayer
{
    public class KickPlayerCommand : CommandBase<CommandResponse>
    {
        public Guid PlayerId { get; set; }
    }
}

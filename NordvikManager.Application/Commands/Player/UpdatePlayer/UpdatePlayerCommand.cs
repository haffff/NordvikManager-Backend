using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.Player.UpdatePlayer
{
    public class UpdatePlayerCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public PlayerDTO playerDTO { get; set; }
    }
}

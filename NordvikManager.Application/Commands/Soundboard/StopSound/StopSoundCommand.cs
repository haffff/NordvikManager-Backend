using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.Soundboard.StopSound
{
    public class StopSoundCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid ResourceId { get; set; }
    }
}

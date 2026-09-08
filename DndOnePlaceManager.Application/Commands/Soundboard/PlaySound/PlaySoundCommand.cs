using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Soundboard.PlaySound
{
    public class PlaySoundCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public Guid ResourceId { get; set; }
    }
}

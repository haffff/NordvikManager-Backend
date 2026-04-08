using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Player.KickPlayer
{
    public class KickPlayerCommand : CommandBase<CommandResponse>
    {
        public Guid PlayerId { get; set; }
    }
}

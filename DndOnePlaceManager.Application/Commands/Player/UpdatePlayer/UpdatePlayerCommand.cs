using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.Player.UpdatePlayer
{
    public class UpdatePlayerCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public PlayerDTO playerDTO { get; set; }
    }
}

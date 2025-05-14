using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer
{
    public class GetSystemPlayerCommand : CommandBase<PlayerDTO>
    {
        public Guid? GameID { get; set; }
    }
}


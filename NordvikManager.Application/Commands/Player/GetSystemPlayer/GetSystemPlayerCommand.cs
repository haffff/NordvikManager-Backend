using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.Game.Player.GetPlayer
{
    public class GetSystemPlayerCommand : CommandBase<PlayerDTO>
    {
        public Guid? GameID { get; set; }
    }
}


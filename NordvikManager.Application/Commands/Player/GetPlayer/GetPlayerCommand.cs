using NordvikManager.Domain.Entities.Auth;

namespace NordvikManager.Application.Commands.Game.Player.GetPlayer
{
    public class GetPlayerCommand : CommandBase<GetPlayerCommandResponse>
    {
        public Guid? GameID { get; set; }
        public User? User { get; set; }
    }
}


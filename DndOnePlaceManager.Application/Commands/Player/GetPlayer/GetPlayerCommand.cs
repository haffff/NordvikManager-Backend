using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer
{
    public class GetPlayerCommand : CommandBase<GetPlayerCommandResponse>
    {
        public Guid? GameID { get; set; }
        public User? User { get; set; }
    }
}


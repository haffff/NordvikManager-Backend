
using DNDOnePlaceManager.Domain.Entities.Auth;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class GetGameListCommand : CommandBase<GetGameListCommandResponse>
    {
        public string UserId { get; set; }
    }
}


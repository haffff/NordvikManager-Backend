
using DndOnePlaceManager.Application.Commands.BattleMap.GetGame;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class GetGameCommand : CommandBase<GetGameCommandResponse>
    {
        public Guid GameID { get; set; }
        public Guid PlayerID { get; set; }
    }
}


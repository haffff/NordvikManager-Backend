
using NordvikManager.Application.Commands.BattleMap.GetGame;
using NordvikManager.Domain.Entities.BattleMap;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class GetGameCommand : CommandBase<GetGameCommandResponse>
    {
        public Guid GameID { get; set; }
        public Guid PlayerID { get; set; }
    }
}


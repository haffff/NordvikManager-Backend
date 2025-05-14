using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class UpdateBattleMapCommand : CommandBase<CommandResponse>
    {
        public BattleMapDto Dto { get; set; }
        public PlayerDTO Player { get; set; }
    }
}


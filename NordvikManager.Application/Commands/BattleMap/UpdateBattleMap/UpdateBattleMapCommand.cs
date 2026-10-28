using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class UpdateBattleMapCommand : CommandBase<CommandResponse>
    {
        public BattleMapDto Dto { get; set; }
        public PlayerDTO Player { get; set; }
    }
}


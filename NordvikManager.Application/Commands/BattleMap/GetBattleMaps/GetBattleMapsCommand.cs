using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Generic.Command;
using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class GetBattleMapsCommand : GenericGetListCommand<BattleMapDto>
    {
    }
}

using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.BattleMap
{
    public class GetGameListCommandResponse
    {
        public List<GameItemDTO>? GameItemList { get; set; }
    }
}


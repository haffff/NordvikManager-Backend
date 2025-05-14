
using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.BattleMap
{
    public class GetGameListCommandResponse
    {
        public List<GameItemDTO>? GameItemList { get; set; }
    }
}


using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.BattleMap.GetGame
{
    public class GetGameCommandResponse
    {
        public Guid Id { get; set; }
        public PlayerDTO? Master { get; set; }
        public List<MapDTO>? Maps { get; set; }
        public List<PlayerDTO>? Players { get; set; }
        public string? Name { get; set; }
        public MapDTO? SelectedMap { get; set; }
        public LayoutDTO DefaultLayout { get; set; }
        public List<LayoutDTO> Layouts { get; set; }
        public Permission? Permission { get; set; }
        public List<BattleMapDto> BattleMaps { get; set; }
        public bool RequirePassword { get; set; }
        public object Properties { get; internal set; }

        public bool? UseSquaredSystem { get; set; }
        public int? BaseDistancePerSquare { get; set; }
        public string BaseDistanceUnit { get; set; }
    }
}

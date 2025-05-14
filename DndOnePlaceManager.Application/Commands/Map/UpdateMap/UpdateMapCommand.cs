using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Map.UpdateMap
{
    public class UpdateMapCommand : CommandBase<CommandResponse>
    {
        public Guid GameId { get; set; }
        public MapDTO Map { get; set; }
        public PlayerDTO Player { get; set; }
    }
}

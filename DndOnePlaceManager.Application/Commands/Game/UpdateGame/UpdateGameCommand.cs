using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Game.UpdateGame
{
    public class UpdateGameCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO Player { get; set; }
        public Guid GameId { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string? BaseDistanceUnit { get; set; }
        public int? BaseDistancePerSquare { get; set; }
        public bool? UseSquaredSystem { get; set; }
    }
}

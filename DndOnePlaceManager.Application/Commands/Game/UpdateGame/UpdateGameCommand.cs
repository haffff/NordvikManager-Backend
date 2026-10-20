using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.Commands.Game.UpdateGame
{
    public class UpdateGameCommand : CommandBase<CommandResponse>
    {
        public PlayerDTO? Player { get; set; }
        /// <summary>The server's local admin, who may edit any game without being in it.</summary>
        public bool AsLocalAdmin { get; set; }
        public Guid GameId { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string? BaseDistanceUnit { get; set; }
        public int? BaseDistancePerSquare { get; set; }
        public bool? UseSquaredSystem { get; set; }
        /// <summary>The game list's description (the "shortDescription" game property).</summary>
        public string? ShortDescription { get; set; }
    }
}

namespace DndOnePlaceManager.Application.Commands.Game.GameExists
{
    /// <summary>Whether a game with this id exists (nothing else is loaded).</summary>
    public class GameExistsCommand : CommandBase<bool>
    {
        public Guid GameId { get; set; }
    }
}

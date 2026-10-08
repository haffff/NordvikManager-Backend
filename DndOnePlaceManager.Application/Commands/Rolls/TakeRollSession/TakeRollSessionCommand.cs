using DndOnePlaceManager.Application.Services.Rolls;

namespace DndOnePlaceManager.Application.Commands.Rolls.TakeRollSession
{
    /// <summary>
    /// Removes and returns a pending roll session, or null if it's unknown, expired,
    /// or not this player's in this game (someone else's session is left untouched).
    /// </summary>
    public class TakeRollSessionCommand : CommandBase<RollSession?>
    {
        public Guid RollId { get; set; }
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
    }
}

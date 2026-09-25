using DndOnePlaceManager.Application.Services.Rolls;

namespace DndOnePlaceManager.Application.Commands.Rolls.StartRoll
{
    /// <summary>
    /// Evaluates a batch of formulas server-side WITHOUT posting anything and holds
    /// the results as a roll session (see RollsController). Throws
    /// InvalidRollFormulaException naming the first formula that can't be evaluated,
    /// in which case no session is created.
    /// </summary>
    public class StartRollCommand : CommandBase<RollSession>
    {
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public IReadOnlyList<(string Key, string Formula)> Formulas { get; set; } = Array.Empty<(string, string)>();
    }
}

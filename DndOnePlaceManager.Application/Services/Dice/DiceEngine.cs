using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;

namespace DndOnePlaceManager.Application.Services.Dice
{
    // Supports standard NdM notation with +-*/ arithmetic, keep/drop highest/lowest
    // (khN/klN/dhN/dlN), exploding dice (!), success/failure counting (cs>N/cf<N),
    // and Fudge/Fate dice (dF).
    public class DiceEngine : IDiceEngine
    {
        private readonly IDiceRandomSource _random;

        public DiceEngine(IDiceRandomSource random)
        {
            _random = random;
        }

        public RollDefinition Evaluate(string expression) =>
            new DiceExpressionEvaluator(_random).Evaluate(expression);
    }
}

namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class DiceDefinition
    {
        public int Index { get; set; }
        public int DiceValue { get; set; }
        public int Times { get; set; }
        public int Result { get; set; }
        public DiceDefinition(int diceValue, int times, int result, int index)
        {
            DiceValue = diceValue;
            Times = times;
            Result = result;
            Index = index;
        }

        // Additive — default values match every pre-existing caller of the 4-arg
        // constructor above (a plain NdM roll: nothing dropped, nothing exploded,
        // no success/fail threshold), so old serialized rolls and old consumers are
        // unaffected. Only set explicitly by the new dice engine's evaluator.
        public bool Kept { get; init; } = true;
        public bool Exploded { get; init; } = false;
        public bool? Success { get; init; } = null;
    }
}

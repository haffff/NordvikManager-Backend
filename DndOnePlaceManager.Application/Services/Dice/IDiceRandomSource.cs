namespace DndOnePlaceManager.Application.Services.Dice
{
    // Seam over randomness so dice-evaluation logic (keep/drop, exploding, success/fail
    // thresholds) can be unit-tested with a scripted sequence instead of real randomness.
    public interface IDiceRandomSource
    {
        // Returns a value in [1, sides].
        int Roll(int sides);
    }
}

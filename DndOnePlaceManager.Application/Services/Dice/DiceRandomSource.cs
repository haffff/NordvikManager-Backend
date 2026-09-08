namespace DndOnePlaceManager.Application.Services.Dice
{
    public class DiceRandomSource : IDiceRandomSource
    {
        public int Roll(int sides) => System.Random.Shared.Next(1, sides + 1);
    }
}

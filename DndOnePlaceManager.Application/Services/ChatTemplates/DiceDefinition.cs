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
    }
}

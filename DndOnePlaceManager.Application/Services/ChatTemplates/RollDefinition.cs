namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class RollDefinition
    {
        public int Result { get; set; }
        public string Rolled { get; set; }
        public DiceDefinition[] Dices { get; set; }
    }
}

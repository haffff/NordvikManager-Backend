using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RollDiceStepData
    {
        [Description("Dice notation to roll, e.g. '1d20', '2d6+3'. Supports variable references.")]
        public string DiceString { get; set; }

        [Description("Name of the variable where the roll result will be stored.")]
        public string OutputVariable { get; set; }
    }
}
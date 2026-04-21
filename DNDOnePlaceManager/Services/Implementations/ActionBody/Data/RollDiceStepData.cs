using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RollDiceStepData
    {
        [Description("Dice notation to roll, e.g. '1d20', '2d6+3'. Supports %variable% substitution.")]
        public string DiceString { get; set; }

        [Description("Name of the variable where the result is stored. Leave empty to skip storing.")]
        public string? OutputVariable { get; set; }

        [Description("When true, stores only the integer total in OutputVariable. When false (default), stores the full roll object — fields accessible via %v:OutputVariable.Result% and %v:OutputVariable.Rolled% in subsequent steps.")]
        public bool SimpleOutput { get; set; }

        [Description("When true, broadcasts the roll result to the game chat using the options below.")]
        public bool PrintToChat { get; set; }

        [ShowIf("PrintToChat", "true")]
        [Description("Title shown on the chat roll card. Supports %variable% substitution. Default: 'Roll'. Note: the current roll's result is not yet available here — use a separate SendChat step after this one if you need %v:var.Result% in the title.")]
        public string? ChatTitle { get; set; }

        [ShowIf("PrintToChat", "true")]
        [Description("Additional message shown below the roll result in chat. Supports %variable% substitution.")]
        public string? ChatMessage { get; set; }

        [ShowIf("PrintToChat", "true")]
        [Description("Background colour of the chat card (CSS colour, e.g. '#1a1a2e'). Leave empty for default.")]
        public string? ChatColor { get; set; }

        [ShowIf("PrintToChat", "true")]
        [Description("Border colour of the chat card (CSS colour, e.g. '#e94560'). Leave empty for default.")]
        public string? ChatBorderColor { get; set; }
    }
}

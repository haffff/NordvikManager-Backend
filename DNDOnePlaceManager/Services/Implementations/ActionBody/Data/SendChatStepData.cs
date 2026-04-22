using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SendChatStepData
    {
        [Description("Template type: 'Roll', 'BigNumber', or 'Text'.")]
        public string Template { get; set; } = "Text";

        [ShowIf("Template", "Roll")]
        [Description("Variable name (without %) holding the RollDefinition from a previous RollDice step.")]
        public string? RollVariable { get; set; }

        [ShowIf("Template", "BigNumber")]
        [Description("The number to display prominently. Supports %variable% substitution (e.g. %v:rollResult.Result%).")]
        public string? Number { get; set; }

        [Description("Title shown on the chat card. Supports %variable% substitution (e.g. %v:rollResult.Result%).")]
        public string? Title { get; set; }

        [Description("Message body shown below the main content. Supports %variable% substitution.")]
        public string? Message { get; set; }

        [Description("Background colour of the chat card (CSS colour, e.g. '#1a1a2e'). Leave empty for default.")]
        public string? Color { get; set; }

        [Description("Border colour of the chat card (CSS colour, e.g. '#e94560'). Leave empty for default.")]
        public string? BorderColor { get; set; }
    }
}

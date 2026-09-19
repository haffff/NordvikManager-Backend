using DNDOnePlaceManager.Models;
using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RequestUserInputStepData
    {
        [Description("Username of the player to request input from. Supports %variable% substitution (e.g. %playerId%).")]
        public string? UserName { get; set; }

        [UIType("playerid")]
        [Description("Player to request input from. Pick one, or type an ID / %variable%. Takes precedence over UserName if both are set.")]
        public string? UserID { get; set; }

        [Description("Message or prompt displayed to the user when requesting input. Supports %variable% substitution.")]
        public string? Message { get; set; }

        [Description("When enabled, the targeted player is shown a built-in modal with a text box, so " +
                     "they can answer without a custom addon frontend. When disabled, only addons " +
                     "subscribed to the 'request_input' command handle this step.")]
        public bool ShowDialog { get; set; }

        [ShowIf("ShowDialog", "true")]
        [Description("Pre-fills the built-in dialog's text box. Supports %variable% substitution. " +
                     "Leave empty for a blank box.")]
        public string? DefaultInput { get; set; }

        [Description("Maximum time to wait for user input before the step times out.")]
        public TimeSpan? Timeout { get; set; }

        [Description("Name of the variable where the user's input will be stored.")]
        public string? Output { get; set; }
    }
}

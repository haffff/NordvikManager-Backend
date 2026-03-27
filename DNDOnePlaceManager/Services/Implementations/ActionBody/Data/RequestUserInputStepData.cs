using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RequestUserInputStepData
    {
        [Description("Username of the player to request input from.")]
        public string? UserName { get; set; }

        [Description("ID of the player to request input from. Takes precedence over UserName if both are set.")]
        public string? UserID { get; set; }

        [Description("Message or prompt displayed to the user when requesting input.")]
        public string? Message { get; set; }

        [Description("Maximum time to wait for user input before the step times out.")]
        public TimeSpan? Timeout { get; set; }

        [Description("Name of the variable where the user's input will be stored.")]
        public string? Output { get; set; }
    }
}

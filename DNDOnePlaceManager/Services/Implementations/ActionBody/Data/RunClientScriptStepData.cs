using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RunClientScriptStepData
    {
        [Description("Name of the client-side script to execute.")]
        public string? Script { get; set; }

        [Description("Username or variable name of the player on whose client the script will run. Leave empty to run for all players.")]
        public string? Player { get; set; }

        [Description("Comma-separated list of variable names to pass as arguments to the script.")]
        public string? Arguments { get; set; }
    }
}

using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    // Same JSON shape as ValueStepData ("Value"), but with its own metadata for the editor.
    public class ExecuteActionStepData
    {
        [UIType("action")]
        [Description("Action to run, as 'prefix/name'. It gets a copy of the current variables.")]
        public string Value { get; set; }
    }

    public class GetConnectedPlayersStepData
    {
        [VariableOutput]
        [Description("Name of the variable that receives the list of connected players.")]
        public string Value { get; set; }
    }
}

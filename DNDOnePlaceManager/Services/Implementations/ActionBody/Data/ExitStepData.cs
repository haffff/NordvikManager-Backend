using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ExitStepData
    {
        [Description("Optional message written to the game event log when the action stops. Leave empty to stop silently.")]
        public string? Message { get; set; }
    }
}

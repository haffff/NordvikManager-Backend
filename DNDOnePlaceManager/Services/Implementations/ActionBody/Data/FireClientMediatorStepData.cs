using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class FireClientMediatorStepData
    {
        [Description("Name of the client mediator event to fire.")]
        public string EventName { get; set; }

        [Description("JSON payload to pass with the event. Supports %variable% substitution.")]
        public string Payload { get; set; }

        [Description("Player name or ID to target. Supports %variable% substitution (e.g. %playerId%). Leave empty to broadcast to all.")]
        public string Player { get; set; }
    }
}

using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ShowViewStepData
    {
        [Description("ID or name of the view to show.")]
        public string ViewId { get; set; }

        [Description("Player name or ID to send the view to. Leave empty to broadcast to all.")]
        public string Player { get; set; }

        [Description("Optional JSON data to pass to the view.")]
        public string Data { get; set; }
    }
}

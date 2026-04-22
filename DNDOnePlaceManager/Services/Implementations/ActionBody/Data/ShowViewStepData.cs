using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ShowViewStepData
    {
        [Description("Key of the view to show, as declared in the addon's view JSON (e.g. \"dnd5e_character_sheet\").")]
        public string ViewKey { get; set; }

        [Description("Player name or ID to send the view to. Supports %variable% substitution (e.g. %playerId%). Leave empty to broadcast to all.")]
        public string Player { get; set; }

        [Description("Optional JSON data to pass to the view. Supports %variable% substitution.")]
        public string Data { get; set; }
    }
}

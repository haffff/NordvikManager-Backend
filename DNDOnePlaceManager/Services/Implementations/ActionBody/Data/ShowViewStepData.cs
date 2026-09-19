using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ShowViewStepData
    {
        [Description("Key of the view to show, as declared in the addon's view JSON (e.g. \"dnd5e_character_sheet\").")]
        public string ViewKey { get; set; }

        [UIType("playerid")]
        [Description("Player to send the view to. Pick one, or type a name / ID / %variable%. Leave empty to broadcast to all.")]
        public string Player { get; set; }

        [Description("Optional JSON data to pass to the view. Supports %variable% substitution.")]
        public string Data { get; set; }
    }
}

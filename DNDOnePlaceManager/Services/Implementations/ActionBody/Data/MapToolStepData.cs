using DNDOnePlaceManager.Models;
using System.Collections.Generic;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class MapToolStepData
    {
        [Description("Id of this tool. Running the step again with the same Name does not add a duplicate.")]
        public string Name { get; set; }

        [Description("Label shown in the Tools panel under 'Addon tools'. Defaults to Name when empty.")]
        public string UiName { get; set; }

        [UIType("action")]
        [Description("Action to run when the tool is used on the map, as 'prefix/name'. It gets the variables battleMapId, mapId, position " +
            "({x, y} on the map) and elementId (the clicked element, if any), plus ActionArgs.")]
        public string Action { get; set; }

        [UIType("textarea")]
        [Description("Text shown on the map while the tool is active, e.g. 'Click a token to teleport it'.")]
        public string Hint { get; set; }

        [Description("Which clicks use the tool: 'point' (anywhere, default), 'token' (only on a token) or 'element' (on any element). " +
            "Clicks that don't match are ignored.")]
        public string Target { get; set; }

        [Description("If true, the tool stays active after a click until the player presses Stop or picks another tool. " +
            "Otherwise it goes back to Select after one use.")]
        public bool StayActive { get; set; }

        [Description("If true, only the player who triggered this action gets the tool; otherwise every connected player does.")]
        public bool OnlyOwner { get; set; }

        [Description("Optional key/value arguments passed to the action when the tool is used. Accessible as variables in that action.")]
        public Dictionary<string, object> ActionArgs { get; set; }
    }
}

using System.Collections.Generic;

namespace DndOnePlaceManager.Application.Services.Implementations.ChatTemplates
{
    public class ActionItemTemplate
    {
        public string Label { get; set; }
        public string ActionName { get; set; }

        // Baked-in arguments sent along with ActionName when this button is clicked,
        // e.g. a weapon's damage dice/type captured at attack-roll time so "Roll Damage"
        // doesn't need to re-derive them later.
        public Dictionary<string, object> Args { get; set; }
    }

    public class ActionTemplate : ChatTemplate
    {
        public override string Type => "Action";
        public ActionItemTemplate[] Actions { get; set; }
    }
}

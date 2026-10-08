using DNDOnePlaceManager.Models;
using System.Collections.Generic;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class AddToolbarButtonStepData
    {
        [Description("Unique identifier for the toolbar button.")]
        public string Name { get; set; }

        [Description("Display label for the button.")]
        public string UiName { get; set; }

        [Description("Not shown yet: toolbar buttons are text only.")]
        public string Icon { get; set; }

        [UIType("action")]
        [Description("Action to run when the button is clicked, as 'prefix/name'. It receives ActionArgs as variables. Not used when MenuId is set (the button then opens a menu).")]
        public string Action { get; set; }

        [Description("Not used yet: buttons are always added at the end of the main toolbar.")]
        public string Location { get; set; }

        [Description("If true, only the player who triggered this action gets the button; otherwise every connected player does.")]
        public bool OnlyOwner { get; set; }

        [Description("If set, the button opens a dropdown menu with this id instead of running an action. Fill it with Add Menu Item steps that use this id as their Location, e.g. 'myaddon_menu'.")]
        public string MenuId { get; set; }

        [Description("Display name for the dropdown menu button (used when MenuId is set).")]
        public string MenuName { get; set; }

        [Description("Optional key/value arguments passed to the action when the button is clicked. Accessible as variables in the triggered action.")]
        public Dictionary<string, object> ActionArgs { get; set; }
    }
}

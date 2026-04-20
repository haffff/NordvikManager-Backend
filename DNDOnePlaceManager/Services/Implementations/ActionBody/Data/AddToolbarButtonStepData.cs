using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class AddToolbarButtonStepData
    {
        [Description("Unique identifier for the toolbar button.")]
        public string Name { get; set; }

        [Description("Display label for the button.")]
        public string UiName { get; set; }

        [Description("Icon identifier for the button.")]
        public string Icon { get; set; }

        [Description("Action name to execute when button is clicked.")]
        public string Action { get; set; }

        [Description("Toolbar section/group to place the button in.")]
        public string Location { get; set; }

        [Description("If true, sends the button only to the triggering player.")]
        public bool OnlyOwner { get; set; }

        [Description("If non-empty, this button becomes a dropdown menu with this viewId. Addon actions can then add items to it via AddMenuItemStep with Location set to this value.")]
        public string MenuId { get; set; }

        [Description("Display name for the dropdown menu button (used when MenuId is set).")]
        public string MenuName { get; set; }
    }
}

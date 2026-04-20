namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class MenuButtonStepData
    {
        public string Name { get; set; }
        public string UiName { get; set; }
        public string Icon { get; set; }
        public string Action { get; set; }
        public string Location { get; set; }
        public bool OnlyOwner { get; set; }

        [System.ComponentModel.Description("If non-empty, places this item inside a submenu with this viewId. Example: 'addons.myaddon'. The submenu is created on the client if it does not yet exist.")]
        public string SubMenuId { get; set; }

        [System.ComponentModel.Description("Display name for the submenu (used only when SubMenuId is set and the submenu does not yet exist).")]
        public string SubMenuName { get; set; }
    }
}

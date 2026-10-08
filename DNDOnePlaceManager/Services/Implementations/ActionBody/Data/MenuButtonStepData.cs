using DNDOnePlaceManager.Models;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class MenuButtonStepData
    {
        [System.ComponentModel.Description("Id of this menu item, unique within its menu. Running the step again with the same Name does not add a duplicate.")]
        public string Name { get; set; }

        [System.ComponentModel.Description("Text shown in the menu. Defaults to Name when empty.")]
        public string UiName { get; set; }

        [System.ComponentModel.Description("Not shown yet: menu items are text only.")]
        public string Icon { get; set; }

        [UIType("action")]
        [System.ComponentModel.Description("Action to run when the item is clicked, as 'prefix/name'. It receives ActionArgs as variables.")]
        public string Action { get; set; }

        [UIType("menulocation")]
        [System.ComponentModel.Description("Menu to add the item to: pick a built-in menu, or type the Menu Id of a toolbar dropdown made with Add Toolbar Button. " +
            "Empty = the Game menu. Right-click menus pass what was clicked as variables: 'battlemap_add' (map > Add) and 'battlemap' (empty map space) " +
            "give battleMapId and position, 'battlemap_element' (a token/element) also elementId, 'cards_item' (a card in the Cards panel) gives cardId. " +
            "Players can open these menus too, so check permissions in the action (Require Permission).")]
        public string Location { get; set; }

        [System.ComponentModel.Description("If true, only the player who triggered this action gets the item; otherwise every connected player does.")]
        public bool OnlyOwner { get; set; }

        [System.ComponentModel.Description("If non-empty, places this item inside a submenu with this viewId. Example: 'addons.myaddon'. The submenu is created on the client if it does not yet exist.")]
        public string SubMenuId { get; set; }

        [System.ComponentModel.Description("Display name for the submenu (used only when SubMenuId is set and the submenu does not yet exist).")]
        public string SubMenuName { get; set; }

        [System.ComponentModel.Description("Optional key/value arguments passed to the action when the menu item is clicked. Accessible as variables in the triggered action.")]
        public Dictionary<string, object> ActionArgs { get; set; }
    }
}

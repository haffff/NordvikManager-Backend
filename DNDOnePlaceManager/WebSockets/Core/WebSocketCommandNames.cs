using DndOnePlaceManager.Domain.Enums;

namespace DNDOnePlaceManager.WebSockets.Core
{
    public static class WebSocketCommandNames
    {
        // Element  
        public const string ElementUpdate = "element_update";
        public const string ElementRemove = "element_remove";
        public const string ElementAdd = "element_add";
        public const string ElementGroup = "element_group";
        public const string ElementUngroup = "element_ungroup";

        // Battlemap  
        public const string BattleMapRemove = "battlemap_remove";
        public const string BattleMapAdd = "battlemap_add";
        public const string BattleMapRename = "battlemap_rename";

        // Admin  
        public const string PlayerKick = "player_kick";

        // Actions  
        public const string ActionUpdate = "action_update";
        public const string ActionRemove = "action_remove";
        public const string ActionAdd = "action_add";

        // Card  
        public const string TemplateUpdate = "template_update";
        public const string CustomPanelUpdate = "custom_panel_update";
        public const string CardUpdate = "card_update";
        public const string TemplateDelete = "template_delete";
        public const string CustomPanelDelete = "custom_panel_delete";
        public const string CardDelete = "card_delete";
        public const string TemplateAdd = "template_add";
        public const string CustomPanelAdd = "custom_panel_add";
        public const string CardAdd = "card_add";

        //Layout
        public const string LayoutUpdate = "layout_update";
        public const string LayoutRemove = "layout_remove";
        public const string LayoutAdd = "layout_add";

        //Map
        public const string MapChange = "map_change";
        public const string MapAdd = "map_add";
        public const string MapRemove = "map_remove";

        //Permissions
        public const string PermissionsUpdate = "permission_update";

        //Resources
        public const string ResourceUpdate = "resource_update";
        public const string ResourceDelete = "resource_delete";
        public const string ResourceAdd = "resource_add";

        //Settings
        public const string SettingsGame = "settings_game";
        public const string SettingsMap = "settings_map";
        public const string SettingsPlayer = "settings_player";

        //Tree
        public const string TreeUpdate = "tree_update";
        public const string TreeRemove = "tree_remove";
        public const string TreeAdd = "tree_add";

        //Player
        public const string PlayerJoin = "player_join";
        public const string PlayerLeave = "player_leave";

        //Properties
        public const string PropertyUpdate = "property_update";
        public const string PropertyRemove = "property_remove";
        public const string PropertyAdd = "property_add";

    }
}

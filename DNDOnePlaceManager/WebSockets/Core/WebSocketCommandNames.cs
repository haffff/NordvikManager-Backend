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
        public const string LayoutAdd = "layout_add";        //Map
        public const string MapChange = "map_change";
        public const string MapAdd = "map_add";
        public const string MapUpdate = "map_update";
        public const string MapRemove = "map_remove";

        //Permissions
        public const string PermissionsUpdate = "permission_update";

        //Resources
        public const string ResourceUpdate = "resource_update";
        public const string ResourceDelete = "resource_delete";
        public const string ResourceAdd = "resource_add";

        //Long-running operation progress (addon install, resource linking, ...)
        //Data payloads: OperationProgress = { id, current, total, message }
        //               OperationComplete/OperationFailed = { id, title, description }
        public const string OperationProgress = "operation_progress";
        public const string OperationComplete = "operation_complete";
        public const string OperationFailed = "operation_failed";

        //Sound
        public const string SoundPlay = "sound_play";
        public const string SoundStop = "sound_stop";

        //Playlist
        public const string PlaylistNotify = "playlist_notify";
        public const string PlaylistPlay = "playlist_play";
        public const string PlaylistPause = "playlist_pause";
        public const string PlaylistStop = "playlist_stop";
        public const string PlaylistTrackChange = "playlist_track_change";

        //Settings
        public const string SettingsGame = "settings_game";
        public const string SettingsMap = "settings_map";
        public const string SettingsPlayer = "settings_player";

        //Tree
        public const string TreeUpdate = "tree_update";
        public const string TreeRemove = "tree_remove";
        public const string TreeAdd = "tree_add";        //Player
        public const string PlayerJoin = "player_join";
        public const string PlayerLeave = "player_leave";        //Properties
        public const string PropertyUpdate = "property_update";
        public const string PropertyRemove = "property_remove";
        public const string PropertyAdd = "property_add";

        //Property lists — repeating-row data stored as a JSON array inside a
        //single PropertyModel.Value (see PropertyList command handlers). All four
        //re-broadcast as PropertyUpdate so existing property_update listeners need
        //no changes to pick up list mutations.
        public const string PropertyListItemAdd = "property_list_item_add";
        public const string PropertyListItemRemove = "property_list_item_remove";
        public const string PropertyListItemUpdate = "property_list_item_update";
        public const string PropertyListReorder = "property_list_reorder";

        // Custom battle-map layers — a thin, game-scoped wrapper around the same
        // "customLayers" property list. Add/Move allocate numeric layer ids
        // server-side by fitting them between the reserved Map/Grid/Token/TokenUi
        // anchors (see CustomLayerLayout); Remove atomically reassigns any elements
        // on the deleted layer back to the Map layer. All three re-broadcast as
        // PropertyUpdate, same as the four PropertyList commands above.
        public const string CustomLayerAdd = "custom_layer_add";
        public const string CustomLayerRemove = "custom_layer_remove";
        public const string CustomLayerMove = "custom_layer_move";

        // Connection / handshake
        public const string HandshakeOk = "OK";
        public const string LogCommand = "log";
        public const string LobbyJoinFailedReason = "Joining lobby failed";
        public const string UserContextKey = "User";

        // Error responses
        public const string ErrorGeneric = "error";
        public const string ErrorPermission = "error_permission";
        public const string ErrorArguments = "error_arguments";
        public const string ErrorResource = "error_resource";
        public const string ErrorGeneral = "error_general";

        // Result values
        public const string ResultNotAllowed = "Not allowed";
        public const string ResultPass = "Pass";
        public const string ResultOk = "Ok";
        public const string ResultCommandNotFound = "CommandNotFound";
        public const string ResultNoData = "No data provided";

        // Special / system commands
        public const string CmdClientScriptExecute = "clientscript_execute";
        public const string CmdPermissionsUpdate = "permissions_update";
        public const string CmdPlayerList = "player_list";
        public const string CmdClientLoaded = "client_loaded";
        public const string CmdClientLayoutReady = "client_layout_ready";
        public const string CmdDebugModeGet = "debug_mode_get";
        public const string CmdDebugModeSet = "debug_mode_set";
        public const string CmdExecuteAction = "execute_action";
        public const string CmdDebugActionResponse = "debug_action_response";
        public const string CmdInputValue = "input_value";

        // Passthrough preview commands
        public const string CmdPreviewStart = "preview_start";
        public const string CmdPreviewUpdate = "preview_update";
        public const string CmdPreviewEnd = "preview_end";        // JSON data field keys
        public const string DataKeyParentId = "parentId";
        public const string DataKeyId = "id";
        public const string DataKeyPermission = "permission";
        public const string DataKeyAction = "Action";
        public const string DataKeyArgs = "Args";

        // Chat
        public const string CmdChatPush = "chat_push";

        // Client UI (addon sandbox)
        public const string CmdShowView           = "view_show";
        public const string CmdAddMenuItem        = "menu_item_add";
        public const string CmdAddToolbarButton   = "toolbar_button_add";
        public const string CmdFireClientMediator = "client_mediator_fire";

        // Debug / action engine
        public const string CmdDebugAction = "debug_action";
        public const string StepTypeExit = "Exit";
        public const string StepTypeKey = "Type";
        public const string DebugStopSignal = "stop";
        public const string DebugMsgStarting = "Starting Action";
        public const string DebugMsgExecutingStep = "Executing Step \"";
        public const string DebugMsgFinishing = "Finishing Action";
        public const string DebugErrStepNotFound = "Step definition not found";
        public const string ErrFailedToGetActions = "ActionProcessor: Failed to get actions";
        public const int DebugPollingIntervalMs = 500;
    }
}

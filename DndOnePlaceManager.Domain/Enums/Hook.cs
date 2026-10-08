namespace DNDOnePlaceManager.Enums
{
    /// <summary>
    /// List of hooks that are taken by the system.
    /// Each value decorated with <see cref="HookMetaAttribute"/> is exposed to the UI
    /// via <see cref="HookInfo.GetAll"/>.
    /// </summary>
    public enum Hook
    {
        None,

        // ── Addon lifecycle ─────────────────────────────────────────────────
        [HookMeta("Install", "Runs once when an addon is installed. Use it to register actions and required properties.", "Addon", Variables = "AddonKey,GameId,PlayerId")]
        Install,

        [HookMeta("Uninstall", "Runs once when an addon is uninstalled. Use it to clean up actions and properties.", "Addon", Variables = "AddonKey,GameId,PlayerId")]
        Uninstall,

        // ── Player ──────────────────────────────────────────────────────────
        [HookMeta("Player Join", "Fires every time a player connects to the game session.", "Player", Variables = "Player")]
        PlayerJoin,

        [HookMeta("Player Leave", "Fires every time a player disconnects from the game session.", "Player", Variables = "Data,Player,Command")]
        PlayerLeave,

        [HookMeta("Player Load", "Fires when a player finishes loading the game board.", "Player", Variables = "Player")]
        Load,

        // ── Elements ────────────────────────────────────────────────────────
        [HookMeta("Element Added", "Fires when a new element is placed on the map.", "Element", Variables = "Data,Player,Command")]
        ElementAdd,

        [HookMeta("Element Updated", "Fires when an existing element's properties are changed.", "Element", Variables = "Data,Player,Command")]
        ElementUpdate,

        [HookMeta("Element Moved", "Fires when an element is dragged to a new position (in addition to Element Updated). Use %v:Data.id% for the element and %v:Data.object.left% / %v:Data.object.top% for the new position.", "Element", Variables = "Data,Player,Command")]
        ElementMove,

        [HookMeta("Element Removed", "Fires when an element is deleted from the map.", "Element", Variables = "Data,Player,Command")]
        ElementRemove,

        // ── Properties ──────────────────────────────────────────────────────
        [HookMeta("Property Added", "Fires when a new property is added to an entity.", "Property", Variables = "Data,Player,Command")]
        PropertyAdd,

        [HookMeta("Property Updated", "Fires when a property value is changed.", "Property", Variables = "Data,Player,Command")]
        PropertyUpdate,

        [HookMeta("Property Removed", "Fires when a property is deleted from an entity.", "Property", Variables = "Data,Player,Command")]
        PropertyRemove,

        // ── Maps ────────────────────────────────────────────────────────────
        [HookMeta("Map Added", "Fires when a new map is created in the game.", "Map", Variables = "Data,Player,Command")]
        MapAdd,

        [HookMeta("Map Updated", "Fires when a map's settings (name, grid, size) are changed.", "Map", Variables = "Data,Player,Command")]
        MapUpdate,

        [HookMeta("Map Removed", "Fires when a map is deleted from the game.", "Map", Variables = "Data,Player,Command")]
        MapRemove,

        [HookMeta("Map Changed", "Fires when the GM switches the active map in a battle map view. Data contains mapId and battleMapId.", "Map", Variables = "Data,Player,Command")]
        MapChange,

        // ── Game ────────────────────────────────────────────────────────────
        [HookMeta("Game Updated", "Fires when top-level game settings (e.g. name) are saved. Data holds the submitted settings; the password is never included.", "Game", Variables = "Data,Player,Command")]
        GameUpdate,

        // ── Chat ────────────────────────────────────────────────────────────
        [HookMeta("Chat Message", "Fires on every chat message sent by any player.", "Chat", Variables = "Data,Player,Command")]
        ChatMessage,

        [HookMeta("Chat Command", "Fires for a chat slash command that is not built in (/r, /roll, /help). Variables: ChatCommand ('cast'), ChatArgs ('fireball 3'), ChatText, Player. While any action uses this hook, unknown commands are no longer answered with 'Wrong command'.", "Chat", Variables = "ChatCommand,ChatArgs,ChatText,Player")]
        ChatCommand,

        // ── Cards ───────────────────────────────────────────────────────────
        [HookMeta("Card Added", "Fires when a new card is created.", "Card", Variables = "Data,Player,Command")]
        CardAdd,

        [HookMeta("Card Updated", "Fires when a card's content or layout is changed.", "Card", Variables = "Data,Player,Command")]
        CardUpdate,

        [HookMeta("Card Deleted", "Fires when a card is removed.", "Card", Variables = "Data,Player,Command")]
        CardDelete,

        // ── Turn order ──────────────────────────────────────────────────────
        // (Appended: hooks are stored by number in addon actions.)
        [HookMeta("Turn Changed", "Fires when the turn passes to another entry of a map's turn order, or the round changes. Data: mapId, round, currentEntryId and elementId (the current token), both empty when that entry is hidden from players.", "Turn order", Variables = "Data,Player,Command")]
        TurnChange,
    }
}

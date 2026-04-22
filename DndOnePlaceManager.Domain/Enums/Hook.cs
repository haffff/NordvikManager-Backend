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
        [HookMeta("Install", "Runs once when an addon is installed. Use it to register actions and required properties.", "Addon")]
        Install,

        [HookMeta("Uninstall", "Runs once when an addon is uninstalled. Use it to clean up actions and properties.", "Addon")]
        Uninstall,

        // ── Player ──────────────────────────────────────────────────────────
        [HookMeta("Player Join", "Fires every time a player connects to the game session.", "Player")]
        PlayerJoin,

        [HookMeta("Player Leave", "Fires every time a player disconnects from the game session.", "Player")]
        PlayerLeave,

        [HookMeta("Player Load", "Fires when a player finishes loading the game board.", "Player")]
        Load,

        // ── Elements ────────────────────────────────────────────────────────
        [HookMeta("Element Added", "Fires when a new element is placed on the map.", "Element")]
        ElementAdd,

        [HookMeta("Element Updated", "Fires when an existing element's properties are changed.", "Element")]
        ElementUpdate,

        [HookMeta("Element Moved", "Fires when an element is dragged to a new position.", "Element")]
        ElementMove,

        [HookMeta("Element Removed", "Fires when an element is deleted from the map.", "Element")]
        ElementRemove,

        // ── Properties ──────────────────────────────────────────────────────
        [HookMeta("Property Added", "Fires when a new property is added to an entity.", "Property")]
        PropertyAdd,

        [HookMeta("Property Updated", "Fires when a property value is changed.", "Property")]
        PropertyUpdate,

        [HookMeta("Property Removed", "Fires when a property is deleted from an entity.", "Property")]
        PropertyRemove,

        // ── Maps ────────────────────────────────────────────────────────────
        [HookMeta("Map Added", "Fires when a new map is created in the game.", "Map")]
        MapAdd,

        [HookMeta("Map Updated", "Fires when a map's settings (name, grid, size) are changed.", "Map")]
        MapUpdate,

        [HookMeta("Map Removed", "Fires when a map is deleted from the game.", "Map")]
        MapRemove,

        [HookMeta("Map Changed", "Fires when the GM switches the active map in a battle map view. Data contains mapId and battleMapId.", "Map")]
        MapChange,

        // ── Game ────────────────────────────────────────────────────────────
        [HookMeta("Game Updated", "Fires when top-level game settings are modified.", "Game")]
        GameUpdate,

        // ── Chat ────────────────────────────────────────────────────────────
        [HookMeta("Chat Message", "Fires on every chat message sent by any player.", "Chat")]
        ChatMessage,

        [HookMeta("Chat Command", "Fires when a chat message starts with '/' — used for slash-command handling.", "Chat")]
        ChatCommand,

        // ── Cards ───────────────────────────────────────────────────────────
        [HookMeta("Card Added", "Fires when a new card is created.", "Card")]
        CardAdd,

        [HookMeta("Card Updated", "Fires when a card's content or layout is changed.", "Card")]
        CardUpdate,

        [HookMeta("Card Deleted", "Fires when a card is removed.", "Card")]
        CardDelete,
    }
}

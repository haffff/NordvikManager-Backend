namespace DNDOnePlaceManager.Enums
{
    /// <summary>
    /// List of hooks that are taken by the system
    /// </summary>
    public enum Hook
    {
        None,
        /// <summary>
        /// Special hook type. runs only once on installation of addon. used to add other actions and required properties
        /// </summary>
        Install,
        /// <summary>
        /// Special hook type. runs only once on uninstallation of addon. used to remove other actions and properties
        /// </summary>
        Uninstall,

        /// <summary>
        /// On each player joins
        /// </summary>
        PlayerJoin,

        /// <summary>
        /// On each player leaves
        /// </summary>
        PlayerLeave,

        /// <summary>
        /// On player loaded
        /// </summary>
        Load,

        /// <summary>
        /// On new element added to the game
        /// </summary>
        ElementAdd,

        /// <summary>
        /// On element update 
        /// </summary>
        ElementUpdate,

        /// <summary>
        /// On element move 
        /// </summary>
        ElementMove,

        /// <summary>
        /// On element remove
        /// </summary>
        ElementRemove,

        /// <summary>
        /// On property add
        /// </summary>
        PropertyAdd,

        /// <summary>
        /// On property update
        /// </summary>
        PropertyUpdate,

        /// <summary>
        /// 
        /// </summary>
        PropertyRemove,

        MapAdd,
        MapUpdate,
        MapRemove,

        GameUpdate,

        /// <summary>
        /// On chat message
        /// </summary>
        ChatMessage,

        /// <summary>
        /// On chat message but only with / prefix (used for command handling)
        /// </summary>
        ChatCommand,

        CardAdd,

        CardUpdate,

        CardDelete,
    }
}

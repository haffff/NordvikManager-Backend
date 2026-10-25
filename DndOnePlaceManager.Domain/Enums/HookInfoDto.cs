namespace DNDOnePlaceManager.Enums
{
    /// <summary>
    /// Serialisable representation of a <see cref="Hook"/> value sent to the UI.
    /// </summary>
    public class HookInfoDto
    {
        /// <summary>Numeric enum value — used as the key when saving an action.</summary>
        public int Value { get; set; }

        /// <summary>Enum member name (e.g. "PlayerJoin").</summary>
        public string Key { get; set; }

        /// <summary>Human-readable display name from <see cref="HookMetaAttribute"/>.</summary>
        public string Name { get; set; }

        /// <summary>Full description from <see cref="HookMetaAttribute"/>.</summary>
        public string Description { get; set; }

        /// <summary>UI grouping category from <see cref="HookMetaAttribute"/>.</summary>
        public string Category { get; set; }
        /// <summary>Variables this hook gives an action (see HookMetaAttribute.Variables).</summary>
        public string[] Variables { get; set; } = System.Array.Empty<string>();

        /// <summary>What the hook filter means for this hook; empty when it takes none (see HookFilters).</summary>
        public string Filter { get; set; } = "";

        /// <summary>Actions on this hook need a filter.</summary>
        public bool FilterRequired { get; set; }
    }
}

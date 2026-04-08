using System;

namespace DNDOnePlaceManager.Enums
{
    /// <summary>
    /// Attaches human-readable metadata to a <see cref="Hook"/> value so the UI
    /// can display what each hook does without hard-coding descriptions client-side.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class HookMetaAttribute : Attribute
    {
        /// <summary>Short display name shown in the UI hook selector.</summary>
        public string Name { get; }

        /// <summary>Full description of when this hook fires and what data it carries.</summary>
        public string Description { get; }

        /// <summary>
        /// Category used for grouping in the UI (e.g. "Player", "Element", "Map").
        /// </summary>
        public string Category { get; }

        public HookMetaAttribute(string name, string description, string category = "General")
        {
            Name = name;
            Description = description;
            Category = category;
        }
    }
}

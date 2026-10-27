using System;

namespace NordvikManager.Enums
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

        /// <summary>
        /// Comma-separated names of the variables this hook gives an action, e.g. "Data,Player".
        /// Shown in the action editor's variable autocomplete.
        /// </summary>
        public string Variables { get; set; } = "";

        /// <summary>
        /// What this hook's filter (ActionModel.HookFilter) means, shown next to the filter field
        /// in the action editor. Empty: the hook takes no filter. See <see cref="HookFilters"/>.
        /// </summary>
        public string Filter { get; set; } = "";

        /// <summary>An action on this hook must have a filter (it never runs from the hook without one).</summary>
        public bool FilterRequired { get; set; }

        public HookMetaAttribute(string name, string description, string category = "General")
        {
            Name = name;
            Description = description;
            Category = category;
        }
    }
}

using System;

namespace DNDOnePlaceManager.Models
{
    /// <summary>
    /// Overrides the UI widget used for a step-data property.
    /// The value is sent to the frontend as the field's "type" string
    /// (e.g. "textarea", "color", "select").
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class UITypeAttribute : Attribute
    {
        public string Type { get; }
        public UITypeAttribute(string type) => Type = type;
    }
}

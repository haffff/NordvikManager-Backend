using System;

namespace DNDOnePlaceManager.Models
{
    /// <summary>
    /// Marks a step-data property as conditionally visible in the action editor.
    /// The field is shown only when <see cref="Field"/> equals <see cref="Value"/>
    /// (case-insensitive). Booleans are compared as "true"/"false" strings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ShowIfAttribute : Attribute
    {
        public string Field { get; }
        public string Value { get; }

        public ShowIfAttribute(string field, string value)
        {
            Field = field;
            Value = value;
        }
    }
}

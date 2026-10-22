using System;

namespace DNDOnePlaceManager.Models
{
    /// <summary>
    /// Marks a step-data property as conditionally visible in the action editor.
    /// The field is shown only when <see cref="Field"/> equals <see cref="Value"/>, or one of
    /// its values separated by | ("rect|line"), case-insensitive. Booleans are compared as
    /// "true"/"false" strings. An empty <see cref="Field"/> counts as its [DefaultValue]
    /// (an empty Shape is a circle), and one holding a %token% shows the field.
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

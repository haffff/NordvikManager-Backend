using System;

namespace DNDOnePlaceManager.Models
{
    /// <summary>
    /// Marks a step-data property whose value is the NAME of a variable the step creates
    /// (e.g. Output, ItemName). The action editor uses it to know which variables exist
    /// after a step, for autocomplete and "undefined variable" hints.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class VariableOutputAttribute : Attribute
    {
    }
}

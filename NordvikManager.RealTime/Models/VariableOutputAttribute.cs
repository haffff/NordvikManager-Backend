using System;

namespace NordvikManager.Models
{
    /// <summary>
    /// Marks a step-data property whose value is the NAME of a variable the step creates
    /// (e.g. Output, ItemName), and says what that variable holds. The action editor uses it
    /// to know which variables exist after a step, and to show their fields before and after
    /// a run.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class VariableOutputAttribute : Attribute
    {
        /// <param name="kind">
        /// <c>value</c> (text or a number), <c>object</c> (with <paramref name="fields"/>),
        /// <c>list</c> (of objects with <paramref name="fields"/>), <c>item</c> (one item of the
        /// list in <see cref="ItemOf"/>) or <c>script</c> (what a script returns).
        /// </param>
        public VariableOutputAttribute(string kind, params string[] fields)
        {
            Kind = kind;
            Fields = fields;
        }

        public string Kind { get; }
        public string[] Fields { get; }

        /// <summary>The argument whose value decides the fields (Find's What); see <see cref="FieldsFrom"/>.</summary>
        public string? FieldsBy { get; set; }

        /// <summary>A type with a static <c>Fields</c> dictionary: argument value → fields.</summary>
        public Type? FieldsFrom { get; set; }

        /// <summary>A true/false argument that makes a list one item (Find's First).</summary>
        public string? SingleWhen { get; set; }

        /// <summary>A true/false argument that makes an object a plain value (RollDice's SimpleOutput).</summary>
        public string? ValueWhen { get; set; }

        /// <summary>For <c>item</c>: the argument naming the list it is an item of.</summary>
        public string? ItemOf { get; set; }
    }
}

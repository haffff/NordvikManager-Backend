using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetVariableStepData
    {
        [Description("Name of the variable to create or overwrite.")]
        public string? Name { get; set; }

        [Description("Value to assign to the variable. Supports %variable% and %q:...% substitution.")]
        public string? Value { get; set; }

        [Description("Fallback value used when Value resolves to empty string (e.g. property not found). Supports %variable% substitution.")]
        public string? DefaultValue { get; set; }

        [Description("Data type to cast the value to: 'string' (default), 'int', 'long', 'float', 'double', 'bool'.")]
        public string? Type { get; set; }
    }
}

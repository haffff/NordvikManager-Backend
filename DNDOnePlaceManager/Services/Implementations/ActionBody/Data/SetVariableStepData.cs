using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetVariableStepData
    {
        [Description("Name of the variable to create or overwrite.")]
        public string? Name { get; set; }

        [Description("Value to assign to the variable. Supports variable references.")]
        public string? Value { get; set; }

        [Description("Data type of the value, e.g. 'string', 'int', 'bool', 'float'.")]
        public string? Type { get; set; }
    }
}

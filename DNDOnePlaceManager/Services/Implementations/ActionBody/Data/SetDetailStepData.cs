using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetDetailStepData
    {
        [Description("Name of the variable holding the entity whose detail will be set.")]
        public string? Input { get; set; }

        [Description("Name of the detail/property to set on the entity.")]
        public string? DetailName { get; set; }

        [Description("Value to assign to the detail. Supports variable references.")]
        public string? Value { get; set; }

        [Description("Data type of the value being set, e.g. 'string', 'int', 'bool'.")]
        public string? Type { get; set; }

        [Description("If true, treats the input as a collection element rather than a top-level entity.")]
        public bool isElement { get; set; }
    }
}

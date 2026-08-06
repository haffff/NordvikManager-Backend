using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetPropertyValueStepData
    {
        [Description("Entity id (or {varName}-style variable reference) whose property to read.")]
        public string ParentId { get; set; }

        [Description("Name of the property to read.")]
        public string PropertyName { get; set; }

        [Description("Name of the variable where the property's value will be stored.")]
        public string Output { get; set; }

        [Description("Value to store when the property doesn't exist. Not substituted like other fields.")]
        public string DefaultValue { get; set; }
    }
}

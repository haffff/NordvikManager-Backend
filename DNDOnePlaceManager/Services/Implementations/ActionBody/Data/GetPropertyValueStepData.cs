using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetPropertyValueStepData
    {
        [Description("GUID of the entity whose property to read, e.g. %v:Data.id% or %playerId%.")]
        public string ParentId { get; set; }

        [Description("Name of the property to read.")]
        public string PropertyName { get; set; }

        [VariableOutput]
        [Description("Name of the variable where the property's value will be stored.")]
        public string Output { get; set; }

        [Description("Value to store when the property doesn't exist. Not substituted like other fields.")]
        public string DefaultValue { get; set; }
    }
}

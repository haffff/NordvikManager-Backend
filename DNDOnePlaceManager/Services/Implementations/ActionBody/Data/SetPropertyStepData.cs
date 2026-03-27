using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetPropertyStepData
    {
        [Description("Name of the property to set on the entity.")]
        public string PropertyName { get; set; }

        [Description("Value to assign to the property. Supports variable references.")]
        public string PropertyValue { get; set; }

        [Description("Name of the variable holding the parent entity on which the property will be set.")]
        public string ParentInputName { get; set; }
    }
}

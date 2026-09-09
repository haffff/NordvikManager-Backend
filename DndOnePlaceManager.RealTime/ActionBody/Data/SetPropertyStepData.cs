using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetPropertyStepData
    {
        [Description("GUID of the parent entity whose property will be set. Supports %variable% substitution (e.g. %playerId%, %qn:Card-mycard.id%).")]
        public string ParentId { get; set; }

        [Description("Name of the property to set on the entity.")]
        public string PropertyName { get; set; }

        [Description("Value to assign to the property. Supports %variable% substitution.")]
        public string PropertyValue { get; set; }
    }
}

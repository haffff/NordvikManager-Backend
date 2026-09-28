using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetPropertiesStepData
    {
        [Description("GUID of the entity whose properties will be set. Supports %variable% substitution (e.g. %newCardId%).")]
        public string ParentId { get; set; }

        [Description("One property per line as 'propertyName=value', e.g. 'item_name=%name%'. Values support %variable%, %v:% and %q:% tokens; " +
            "they are substituted after the lines are split, so a value may safely contain line breaks or '='.")]
        public string Properties { get; set; }
    }
}

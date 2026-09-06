using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class UpdatePropertyListItemStepData
    {
        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property.")]
        public string PropertyName { get; set; }

        [Description("Id of the row to update. Supports %variable% substitution.")]
        public string ItemId { get; set; }

        [Description("Fields to merge into the row, as a JSON object, e.g. {\"value\":\"3\"}. Only the supplied keys change. Supports %variable% substitution.")]
        public string Fields { get; set; }
    }
}

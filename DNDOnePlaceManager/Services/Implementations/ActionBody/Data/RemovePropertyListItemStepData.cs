using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class RemovePropertyListItemStepData
    {
        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property.")]
        public string PropertyName { get; set; }

        [Description("Id of the row to remove. Supports %variable% substitution.")]
        public string ItemId { get; set; }
    }
}

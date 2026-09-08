using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class AddPropertyListItemStepData
    {
        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property (must already exist — use a Set Property step with Value \"[]\" to create it first).")]
        public string PropertyName { get; set; }

        [Description("New row's fields as a JSON object, e.g. {\"name\":\"Hunt\",\"value\":\"2\"}. Supports %variable% substitution.")]
        public string Fields { get; set; }

        [Description("Optional — name of the variable where the new row's generated id will be stored.")]
        public string Output { get; set; }
    }
}

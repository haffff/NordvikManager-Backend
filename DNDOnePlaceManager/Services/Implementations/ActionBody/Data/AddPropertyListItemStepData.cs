using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class AddPropertyListItemStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property (must already exist — use a Set Property step with Value \"[]\" to create it first).")]
        public string PropertyName { get; set; }

        [UIType("textarea")]
        [Description("New row's fields as a JSON object, e.g. {\"name\":\"Hunt\",\"value\":\"2\"}. Supports %variable% substitution.")]
        public string Fields { get; set; }

        [VariableOutput("value")]
        [Description("Optional — name of the variable where the new row's generated id will be stored.")]
        public string Output { get; set; }
    }
}

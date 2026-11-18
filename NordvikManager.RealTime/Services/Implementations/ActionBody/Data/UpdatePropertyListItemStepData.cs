using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class UpdatePropertyListItemStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property.")]
        public string PropertyName { get; set; }

        [Description("Id of the row to update. Supports %variable% substitution.")]
        public string ItemId { get; set; }

        [UIType("textarea")]
        [Description("Fields to merge into the row, as a JSON object, e.g. {\"value\":\"3\"}. Only the supplied keys change. Supports %variable% substitution.")]
        public string Fields { get; set; }
    }
}

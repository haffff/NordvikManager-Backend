using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class RemovePropertyListItemStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the parent entity that owns the list property. Supports %variable% substitution.")]
        public string ParentId { get; set; }

        [Description("Name of the list-typed property.")]
        public string PropertyName { get; set; }

        [Description("Id of the row to remove. Supports %variable% substitution.")]
        public string ItemId { get; set; }
    }
}

using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class SetPropertyStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the parent entity whose property will be set. Supports %variable% substitution (e.g. %playerId%, %q:card[My card].id%).")]
        public string ParentId { get; set; }

        [Description("Name of the property to set on the entity.")]
        public string PropertyName { get; set; }

        [Description("Value to assign to the property. Supports %variable% substitution.")]
        public string PropertyValue { get; set; }

        [Description("Store it as a protected property, e.g. an API key a settings view saves for Send Request's bearer token: no player, card or later step can read it back. A protected property stays protected.")]
        public bool Protected { get; set; }
    }
}

using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class GetPropertyValueStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the entity whose property to read, e.g. %Data.id% or %playerId%.")]
        public string ParentId { get; set; }

        [Description("Name of the property to read.")]
        public string PropertyName { get; set; }

        [VariableOutput]
        [Description("Name of the variable where the property's value will be stored.")]
        public string Output { get; set; }

        [Description("Value to store when the property doesn't exist. Not substituted like other fields.")]
        public string DefaultValue { get; set; }
    }
}

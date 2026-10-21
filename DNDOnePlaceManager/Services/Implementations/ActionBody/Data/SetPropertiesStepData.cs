using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetPropertiesStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the entity whose properties will be set. Supports %variable% substitution (e.g. %newCardId%).")]
        public string ParentId { get; set; }

        [Description("One property per line as 'propertyName=value', e.g. 'item_name=%name%'. Values support %variable%, %variable.field% and %q:% tokens; " +
            "they are substituted after the lines are split, so a value may safely contain line breaks or '='.")]
        [UIType("textarea")]
        public string Properties { get; set; }
    }
}

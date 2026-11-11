using NordvikManager.Models;
using System.ComponentModel;

namespace NordvikManager.Services.Implementations.ActionBody.Data
{
    public class DeleteCardStepData
    {
        [Description("GUID of the card to delete (with its properties).")]
        public string CardId { get; set; }
    }

    public class DeletePropertyStepData
    {
        [UIType("propertyowner")]
        [Description("Whose property: empty for the entity in ParentId; tokenCard for the card of the token in ParentId; map for the map in ParentId (empty: the one the player is looking at); game for the game.")]
        public string? Of { get; set; }

        [Description("GUID of the entity that owns the property.")]
        public string ParentId { get; set; }

        [Description("Name of the property to delete. Nothing happens if it doesn't exist.")]
        public string PropertyName { get; set; }
    }

    public class DelayStepData
    {
        [Description("How long to wait before the next step, in milliseconds (max 60000).")]
        public string Milliseconds { get; set; }
    }

    public class LogStepData
    {
        [Description("Text written to the game event log. Supports %variable% substitution.")]
        public string Message { get; set; }

        [Description("'Info' (default), 'Warning' or 'Error'.")]
        public string? Level { get; set; }
    }
}

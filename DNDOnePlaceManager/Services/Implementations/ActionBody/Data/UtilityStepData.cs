using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class DeleteCardStepData
    {
        [Description("GUID of the card to delete (with its properties).")]
        public string CardId { get; set; }
    }

    public class DeletePropertyStepData
    {
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

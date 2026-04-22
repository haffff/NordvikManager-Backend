using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SetResourceStepData
    {
        [Description("Unique string key for the resource (e.g. 'myaddon.config'). Must be unique within the game.")]
        public string Key { get; set; }

        [Description("Display name for the resource. Used only when creating a new resource.")]
        public string Name { get; set; }

        [Description("Text content to store (JSON, plain text, etc.). Supports %variable% substitution.")]
        public string Content { get; set; }

        [Description("Optional variable name to store the resource's GUID in after the operation.")]
        public string OutputVariable { get; set; }
    }
}

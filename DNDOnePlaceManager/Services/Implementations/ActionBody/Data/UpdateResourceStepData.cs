using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class UpdateResourceStepData
    {
        [Description("Key of the resource to update. Takes priority over ResourceId when both are set.")]
        public string Key { get; set; }

        [Description("GUID of the resource to update. Used when Key is not set.")]
        public string ResourceId { get; set; }

        [Description("New text content to store. Supports %variable% substitution.")]
        public string Content { get; set; }
    }
}

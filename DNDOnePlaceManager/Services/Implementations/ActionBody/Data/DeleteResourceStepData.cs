using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class DeleteResourceStepData
    {
        [Description("Key of the resource to delete. Takes priority over ResourceId when both are set.")]
        public string Key { get; set; }

        [Description("GUID of the resource to delete. Used when Key is not set.")]
        public string ResourceId { get; set; }
    }
}

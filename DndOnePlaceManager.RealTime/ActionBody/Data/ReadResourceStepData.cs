using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ReadResourceStepData
    {
        [Description("Key of the resource to read. Takes priority over ResourceId when both are set.")]
        public string Key { get; set; }

        [Description("GUID of the resource to read. Used when Key is not set.")]
        public string ResourceId { get; set; }

        [Description("Variable name to store the retrieved text content in.")]
        public string OutputVariable { get; set; }
    }
}

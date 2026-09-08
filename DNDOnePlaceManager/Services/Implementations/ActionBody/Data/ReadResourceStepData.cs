using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class ReadResourceStepData
    {
        [Description("Key of the resource to read. Takes priority over ResourceId when both are set.")]
        public string Key { get; set; }

        [UIType("resourceid")]
        [Description("Resource to read. Pick one, or type a GUID / %variable%. Used when Key is not set.")]
        public string ResourceId { get; set; }

        [Description("Variable name to store the retrieved text content in.")]
        public string OutputVariable { get; set; }
    }
}

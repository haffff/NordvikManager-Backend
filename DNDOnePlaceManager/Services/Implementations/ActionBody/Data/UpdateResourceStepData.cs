using DNDOnePlaceManager.Models;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class UpdateResourceStepData
    {
        [Description("Key of the resource to update. Takes priority over ResourceId when both are set.")]
        public string Key { get; set; }

        [UIType("resourceid")]
        [Description("Resource to update. Pick one, or type a GUID / %variable%. Used when Key is not set.")]
        public string ResourceId { get; set; }

        [Description("New text content to store. Supports %variable% substitution.")]
        public string Content { get; set; }

        [Description("MIME type to set on the resource (e.g. 'application/json', 'text/plain'). Left unchanged when not set.")]
        public string MimeType { get; set; }
    }
}

using System;
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

        [Description("MIME type of the resource (e.g. 'application/json', 'text/plain'). Defaults to None when not set.")]
        public string MimeType { get; set; }

        [Description("ID of the folder tree entry to place the resource in. Created at root when not set.")]
        public Guid? FolderId { get; set; }

        [Description("Optional variable name to store the resource's GUID in after the operation.")]
        public string OutputVariable { get; set; }
    }
}

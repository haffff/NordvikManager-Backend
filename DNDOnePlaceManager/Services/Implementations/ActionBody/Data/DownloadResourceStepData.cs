using DNDOnePlaceManager.Models;
using System;
using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class DownloadResourceStepData
    {
        [Description("The URL to download, e.g. an image. Private or local addresses (localhost, 192.168.x.x) work only when the server's owner allows the host in Application Settings → Local network access (or Security:ProxyAllowedPrivateHosts in appsettings).")]
        public string? TargetUrl { get; set; }

        [Description("Parent entity ID used to scope the protected-property lookup. Accepts a literal Guid or a variable name holding a Guid. Defaults to the current game ID when not provided.")]
        public string? ParentId { get; set; }

        [Description("Name of a protected property whose value is sent as the Bearer token in the Authorization header (e.g. an API token). It is never returned.")]
        public string? BearerTokenPropertyName { get; set; }

        [Description("Key of the resource to save it as (e.g. 'myaddon_map.png'). An existing resource with this key is replaced.")]
        public string? Key { get; set; }

        [Description("Display name for a new resource. Empty: the key.")]
        public string? Name { get; set; }

        [Description("MIME type to save it with, e.g. image/png. Empty: the type the server sends.")]
        public string? MimeType { get; set; }

        [Description("ID of the folder tree entry to place a new resource in. Created at root when not set.")]
        public Guid? FolderId { get; set; }

        [VariableOutput("object", "success", "statusCode", "resourceId", "mimeType", "size")]
        [Description("Variable for the result: success, statusCode, resourceId (the saved resource), mimeType (empty when resources don't know the type, e.g. SVG) and size in bytes. Nothing is saved when the download fails.")]
        public string? Output { get; set; }
    }
}

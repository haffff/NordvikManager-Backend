using NordvikManager.Application.DataTransferObjects.Game;

namespace NordvikManager.Application.Commands.Properties.Proxy
{
    /// <summary>
    /// GETs a URL and saves the response, byte for byte, as the game resource Key (created or
    /// replaced, like SetResource). The same address rules as ProxyCommand; the bearer token
    /// comes from a protected property. The body is never returned to the caller.
    /// </summary>
    public class DownloadResourceCommand : CommandBase<DownloadResourceResult>
    {
        public Guid GameId { get; set; }
        public PlayerDTO Player { get; set; }
        public string TargetUrl { get; set; }

        /// <summary>Parent entity ID used to scope the protected-property lookup.</summary>
        public Guid ParentID { get; set; }

        /// <summary>Name of a protected property whose value is sent as the Bearer token.</summary>
        public string? BearerTokenPropertyName { get; set; }

        public string Key { get; set; }
        public string? Name { get; set; }

        /// <summary>The resource's MIME type; empty: the response's Content-Type.</summary>
        public string? MimeType { get; set; }

        public Guid? ParentFolder { get; set; }
    }

    public class DownloadResourceResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public Guid? ResourceId { get; set; }

        /// <summary>The type the resource was saved with; null when resources don't know it (e.g. SVG).</summary>
        public string? MimeType { get; set; }

        public long Size { get; set; }
    }
}

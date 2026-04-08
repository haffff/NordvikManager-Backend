using DndOnePlaceManager.Application.DataTransferObjects.Game;

namespace DndOnePlaceManager.Application.Commands.Properties.Proxy
{
    /// <summary>
    /// Sends a POST request to an external URL, injecting selected protected properties into the body.
    /// The values of included protected properties are erased from the response data — they are only
    /// forwarded to the external target and never returned to the caller.
    /// </summary>
    public class ProxyCommand : CommandBase<ProxyCommandResult>
    {
        public PlayerDTO Player { get; set; }        /// <summary>The external URL to forward the request to.</summary>
        public string TargetUrl { get; set; }

        /// <summary>
        /// HTTP method to use for the outgoing request (GET, POST, PUT, PATCH, DELETE, …).
        /// Defaults to POST. Body is omitted for GET and HEAD requests.
        /// </summary>
        public string HttpMethod { get; set; } = "POST";

        /// <summary>
        /// Names of protected properties to resolve and inject into the outgoing body.
        /// Each matched property is added as a key/value pair using the property Name as key.
        /// </summary>
        public string[]? IncludeProtectedPropertyNames { get; set; }        /// <summary>Parent entity ID used to scope the protected-property lookup.</summary>
        public Guid ParentID { get; set; }

        /// <summary>
        /// Name of a protected property whose value will be used as the Bearer token
        /// in the Authorization header of the outgoing request.
        /// The token value is never returned to the caller.
        /// </summary>
        public string? BearerTokenPropertyName { get; set; }

        /// <summary>
        /// Optional additional body data to merge into the outgoing request alongside the properties.
        /// </summary>
        public Dictionary<string, object?>? ExtraBody { get; set; }
    }

    public class ProxyCommandResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string? ResponseBody { get; set; }
    }
}

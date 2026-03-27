using System.ComponentModel;

namespace DNDOnePlaceManager.Services.Implementations.ActionBody.Data
{
    public class SendRequestStepData
    {
        [Description("The URL to send the request to.")]
        public string TargetUrl { get; set; }

        [Description("HTTP method to use: GET, POST, PUT, PATCH, DELETE. Defaults to POST.")]
        public string HttpMethod { get; set; } = "POST";        
        
        [Description("Parent entity ID used to scope the protected-property lookup. Accepts a literal Guid or a variable name holding a Guid. Defaults to the current game ID when not provided.")]
        public string ParentId { get; set; }

        [Description("Comma-separated names of protected properties whose values will be injected into the request body.")]
        public string IncludeProtectedPropertyNames { get; set; }

        [Description("Name of a protected property whose value will be used as the Bearer token in the Authorization header. The token is never included in the body or returned in the output.")]
        public string BearerTokenPropertyName { get; set; }

        [Description("Name of the variable holding a Dictionary<string,object> of extra key/value pairs to merge into the request body alongside the protected properties.")]
        public string ExtraBodyVariable { get; set; }

        [Description("Name of the variable where the response will be stored. The response object has Success (bool), StatusCode (int) and ResponseBody (string).")]
        public string Output { get; set; }
    }
}

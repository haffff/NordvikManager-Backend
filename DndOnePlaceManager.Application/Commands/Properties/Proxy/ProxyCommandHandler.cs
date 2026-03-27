using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Newtonsoft.Json;

namespace DndOnePlaceManager.Application.Commands.Properties.Proxy
{
    public class ProxyCommandHandler : HandlerBase<ProxyCommand, ProxyCommandResult>
    {
        private readonly IProxyHttpService proxyHttpService;

        public ProxyCommandHandler(IDbContext dbContext, IMapper mapper, IProxyHttpService proxyHttpService)
            : base(dbContext, mapper)
        {
            this.proxyHttpService = proxyHttpService;
        }
        public override async Task<ProxyCommandResult> Handle(ProxyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // Collect all protected property names that need to be resolved
            // (body properties + the bearer token property, deduplicated)
            var allProtectedNames = (request.IncludeProtectedPropertyNames ?? Array.Empty<string>())
                .Concat(request.BearerTokenPropertyName != null ? new[] { request.BearerTokenPropertyName } : Array.Empty<string>())
                .Distinct()
                .ToArray();

            // Resolve all needed protected properties in a single query
            var resolvedProtected = allProtectedNames.Length > 0
                ? dbContext.Properties
                    .Where(p => p.IsProtected
                                && p.ParentID == request.ParentID
                                && allProtectedNames.Contains(p.Name))
                    .ToDictionary(p => p.Name!, p => p.Value)
                : new Dictionary<string, string?>();

            // Build the outgoing body
            var body = new Dictionary<string, object?>();

            // Merge any extra body data provided by the caller
            if (request.ExtraBody != null)
            {
                foreach (var kvp in request.ExtraBody)
                    body[kvp.Key] = kvp.Value;
            }

            // Inject requested protected property values into the body
            if (request.IncludeProtectedPropertyNames?.Length > 0)
            {
                foreach (var name in request.IncludeProtectedPropertyNames)
                {
                    if (resolvedProtected.TryGetValue(name, out var value))
                        body[name] = value;
                }
            }

            // Resolve bearer token from its dedicated protected property (never put in body)
            string? bearerToken = null;
            if (request.BearerTokenPropertyName != null)
                resolvedProtected.TryGetValue(request.BearerTokenPropertyName, out bearerToken); var (success, statusCode, responseBody) = await proxyHttpService.SendJsonAsync(
                request.HttpMethod, request.TargetUrl, body, bearerToken, cancellationToken);

            // Strip all protected names (body properties + token property) from the response
            return new ProxyCommandResult
            {
                Success = success,
                StatusCode = statusCode,
                ResponseBody = StripProtectedValues(responseBody, allProtectedNames)
            };
        }

        /// <summary>
        /// Parses the external response and removes any keys that match protected property names,
        /// ensuring protected values are never leaked back to the caller.
        /// </summary>
        private static string? StripProtectedValues(string? responseBody, string[]? protectedNames)
        {
            if (string.IsNullOrWhiteSpace(responseBody) || protectedNames == null || protectedNames.Length == 0)
                return responseBody;

            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object?>>(responseBody);
                if (obj == null) return responseBody;

                foreach (var name in protectedNames)
                    obj.Remove(name);

                return JsonConvert.SerializeObject(obj);
            }
            catch
            {
                // If the response isn't a JSON object we can't strip — return as-is
                return responseBody;
            }
        }
    }
}

using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DndOnePlaceManager.Infrastructure.Services.Proxy;

namespace DndOnePlaceManager.Application.Commands.Properties.Proxy
{
    public class ProxyCommandHandler : HandlerBase<ProxyCommand, ProxyCommandResult>
    {
        private readonly IProxyHttpService proxyHttpService;
        private readonly ProxyOptions proxyOptions;

        public ProxyCommandHandler(IDbContext dbContext, IMapper mapper, IProxyHttpService proxyHttpService, ProxyOptions proxyOptions)
            : base(dbContext, mapper)
        {
            this.proxyHttpService = proxyHttpService;
            this.proxyOptions = proxyOptions;
        }

        public override async Task<ProxyCommandResult> Handle(ProxyCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            if (!await ProxySupport.IsUrlAllowedAsync(request.TargetUrl, proxyOptions))
            {
                return new ProxyCommandResult { Success = false, StatusCode = 400 };
            }

            // Resolve all needed protected properties (body properties + the bearer token
            // property) in a single query
            var resolvedProtected = ProxySupport.ResolveProtected(dbContext, request.ParentID,
                (request.IncludeProtectedPropertyNames ?? Array.Empty<string>()).Append(request.BearerTokenPropertyName));

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
                resolvedProtected.TryGetValue(request.BearerTokenPropertyName, out bearerToken);

            var (success, statusCode, responseBody) = await proxyHttpService.SendJsonAsync(
                request.HttpMethod, request.TargetUrl, body, bearerToken, cancellationToken);

            // Redact all resolved protected values from the response regardless of structure
            return new ProxyCommandResult
            {
                Success = success,
                StatusCode = statusCode,
                ResponseBody = ProxySupport.Redact(responseBody, resolvedProtected)
            };
        }
    }
}

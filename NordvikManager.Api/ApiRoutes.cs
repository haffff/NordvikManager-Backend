using System.Reflection;

namespace NordvikManager.Api
{
    /// <summary>The [ApiRoute] endpoint methods found in some assemblies, by method and path.</summary>
    public sealed class ApiRoutes
    {
        private readonly Dictionary<string, ApiRoute> _routes;

        private ApiRoutes(Dictionary<string, ApiRoute> routes) => _routes = routes;

        /// <summary>The API's own endpoints (this assembly).</summary>
        public static ApiRoutes Shared { get; } = FromAssemblies(typeof(ApiRoutes).Assembly);

        public IReadOnlyCollection<ApiRoute> All => _routes.Values;

        public static ApiRoutes FromAssemblies(params Assembly[] assemblies)
        {
            var routes = new Dictionary<string, ApiRoute>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t.IsClass && !t.IsAbstract))
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (var attribute in method.GetCustomAttributes<ApiRouteAttribute>())
                    {
                        if (method.ReturnType != typeof(Task<ApiResult>) && method.ReturnType != typeof(ApiResult))
                            throw new InvalidOperationException($"{type.Name}.{method.Name} must return ApiResult or Task<ApiResult>.");
                        var route = new ApiRoute(attribute.Method, attribute.Path, type, method, attribute.NativeOnly, attribute.Reason);
                        if (!routes.TryAdd(Key(route.Method, route.Path), route))
                            throw new InvalidOperationException($"Two endpoints answer {route.Method} {route.Path}.");
                    }
                }
            }
            return new ApiRoutes(routes);
        }

        /// <summary>The route registered with exactly this method and path (template), if any.</summary>
        public ApiRoute? Find(string method, string path)
            => _routes.TryGetValue(Key(method.ToUpperInvariant(), path.Trim('/')), out var route) ? route : null;

        /// <summary>
        /// The route a request path answers to: an exact one, or a template whose {name} segments
        /// match any one segment (their values in <paramref name="routeValues"/>).
        /// </summary>
        public ApiRoute? Match(string method, string path, out Dictionary<string, string> routeValues)
        {
            routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            method = method.ToUpperInvariant();
            path = path.Trim('/');
            if (_routes.TryGetValue(Key(method, path), out var exact))
                return exact;

            var segments = path.Split('/');
            foreach (var route in _routes.Values)
            {
                if (route.Method != method || !route.Path.Contains('{'))
                    continue;
                var template = route.Path.Split('/');
                if (template.Length != segments.Length)
                    continue;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var matches = true;
                for (var i = 0; i < template.Length && matches; i++)
                {
                    if (template[i].StartsWith('{') && template[i].EndsWith('}'))
                        values[template[i][1..^1]] = Uri.UnescapeDataString(segments[i]);
                    else
                        matches = string.Equals(template[i], segments[i], StringComparison.OrdinalIgnoreCase);
                }
                if (matches)
                {
                    routeValues = values;
                    return route;
                }
            }
            return null;
        }

        private static string Key(string method, string path) => method + " " + path;
    }

    public sealed record ApiRoute(string Method, string Path, Type EndpointType, MethodInfo Handler, bool NativeOnly = false, string? Reason = null);
}

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
                        var route = new ApiRoute(attribute.Method, attribute.Path, type, method);
                        if (!routes.TryAdd(Key(route.Method, route.Path), route))
                            throw new InvalidOperationException($"Two endpoints answer {route.Method} {route.Path}.");
                    }
                }
            }
            return new ApiRoutes(routes);
        }

        public ApiRoute? Find(string method, string path)
            => _routes.TryGetValue(Key(method.ToUpperInvariant(), path.Trim('/')), out var route) ? route : null;

        private static string Key(string method, string path) => method + " " + path;
    }

    public sealed record ApiRoute(string Method, string Path, Type EndpointType, MethodInfo Handler);
}

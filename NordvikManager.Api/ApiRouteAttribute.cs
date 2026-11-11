namespace NordvikManager.Api
{
    /// <summary>
    /// The route an endpoint method (returning ApiResult or Task&lt;ApiResult&gt;) answers, as its controller action's (e.g. GET
    /// api/BattleMap/GetPlayer). ApiRouteCoverageTests checks every controller action has one.
    /// </summary>
    /// <remarks>
    /// The method's parameters are bound as ASP.NET Core binds the action's: a <c>User</c> is
    /// the caller, simple types come from the query, one complex type from the JSON body.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class ApiRouteAttribute : Attribute
    {
        public ApiRouteAttribute(string method, string path)
        {
            Method = method.ToUpperInvariant();
            Path = path.Trim('/');
        }

        public string Method { get; }

        public string Path { get; }
    }
}

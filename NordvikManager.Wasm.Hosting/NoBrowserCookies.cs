namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// Every request the browser host makes asks fetch() for <c>credentials: "omit"</c>: it sends
    /// no cookies and keeps none it's given. The host build is served by Central itself, so a
    /// cookie Central set on the host's login would be in the jar every tab of that origin shares:
    /// a player client tab would be logged in as the GM, and its refresh would rotate the token
    /// the host holds. The host authenticates with its own tokens (Bearer, and in the body).
    /// </summary>
    public sealed class NoBrowserCookies : DelegatingHandler
    {
        /// <summary>The request option the browser's HttpClient handler passes on to fetch().</summary>
        public static readonly HttpRequestOptionsKey<IDictionary<string, object>> FetchOptions = new("WebAssemblyFetchOptions");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Options.TryGetValue(FetchOptions, out var options))
            {
                options = new Dictionary<string, object>();
                request.Options.Set(FetchOptions, options);
            }
            options["credentials"] = "omit";
            return base.SendAsync(request, cancellationToken);
        }
    }
}

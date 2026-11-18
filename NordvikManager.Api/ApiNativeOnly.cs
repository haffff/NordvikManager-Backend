namespace NordvikManager.Api
{
    /// <summary>
    /// The native server's controllers that are about running a server, and so have no shared
    /// endpoint: a host without ASP.NET Core answers their routes 501, saying they are a GM
    /// server's. Their logic stays in the controller. ApiRouteCoverageTests reads this list.
    /// Single server-only actions of shared controllers are endpoints with NativeOnly = true.
    /// </summary>
    public static class ApiNativeOnly
    {
        public static IReadOnlyDictionary<string, string> Controllers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // In words for a GM: GET api/Host/Info lists them for a browser host.
            ["Backup"] = "Backs up and restores the whole GM server (its database and files). A browser host keeps its games in the browser's storage instead.",
            ["Meta"] = "Central's details, fetched through the GM server. A browser host asks Central itself.",
            ["ServerSettings"] = "The GM server's settings file. A browser host has fixed settings, including its upload limits.",
            ["Update"] = "Updates the GM server's program. A browser host is updated when its page is.",
        };

        /// <summary>Whether a request path ("api/Backup/...") is one of these controllers'.</summary>
        public static bool IsNativeOnlyPath(string path)
        {
            var segments = path.Trim('/').Split('/');
            return segments.Length >= 2 && string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase)
                && Controllers.ContainsKey(segments[1]);
        }
    }
}

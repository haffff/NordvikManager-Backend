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
            ["Backup"] = "backs up and restores the server's own database and files",
            ["Meta"] = "proxies Central's /api/meta for the GM's page; a browser host can ask Central itself",
            ["ServerSettings"] = "the server's settings file; a browser host has fixed settings (FixedServerSettingsStore)",
            ["Update"] = "updates the server's executable; a browser host is updated by redeploying its page",
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

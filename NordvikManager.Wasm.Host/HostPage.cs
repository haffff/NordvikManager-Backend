using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NordvikManager.Wasm.Hosting;
using NordvikManager.Wasm.WebRtc;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NordvikManager.Wasm.Host
{
    /// <summary>
    /// The GM's page: the host runs in this tab (WasmHost, the browser's WebRTC, signaling to
    /// Central), and gm-page.js is its UI, calling <see cref="Login"/> and <see cref="Call"/>.
    /// Central is the page's own origin (the host served from Central needs no CORS), or
    /// <c>?central=</c>. Runs until the tab closes.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class HostPage
    {
        private static HostGm? _gm;

        [JSImport("start", "gm-page")]
        private static partial void StartPage(string centralUrl);

        public static async Task RunAsync()
        {
            var location = JSHost.GlobalThis.GetPropertyAsJSObject("location")!;
            var central = QueryValue(location.GetPropertyAsString("search") ?? "", "central")
                ?? location.GetPropertyAsString("origin")!;

            await JsBrowserRtc.InitializeAsync();
            var provider = WasmHost.Build(WasmHost.Configuration(central), new JsBrowserRtc());
            _gm = provider.GetRequiredService<HostGm>();

            await JSHost.ImportAsync("gm-page", "../gm-page.js");
            StartPage(central);
            Console.WriteLine($"GM host ready (Central {central}).");

            // The host lives as long as the tab: returning would stop the runtime.
            await Task.Delay(Timeout.Infinite);
        }

        [JSExport]
        internal static async Task<bool> Login(string username, string password) => await _gm!.LoginAsync(username, password);

        [JSExport]
        internal static void Logout() => _gm!.Logout();

        [JSExport]
        internal static string? UserName() => _gm?.User?.UserName;

        /// <summary>An API call as the GM; { status, body } as JSON.</summary>
        [JSExport]
        internal static async Task<string> Call(string method, string path, string? bodyJson)
        {
            var (status, body) = await _gm!.CallAsync(method, path, string.IsNullOrEmpty(bodyJson) ? null : JToken.Parse(bodyJson));
            return JsonConvert.SerializeObject(new { status, body });
        }

        private static string? QueryValue(string search, string name)
        {
            foreach (var pair in search.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (Uri.UnescapeDataString(parts[0]) == name && parts.Length == 2 && parts[1].Length > 0)
                    return Uri.UnescapeDataString(parts[1]).TrimEnd('/');
            }
            return null;
        }
    }
}

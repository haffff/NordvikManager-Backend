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
    /// The GM's host, in the page's worker (host-worker.js): WasmHost, the browser's WebRTC (run by
    /// the page, nordvik-webrtc-worker.js), signaling to Central. The page's gm-page.js is its UI,
    /// calling <see cref="Login"/> and <see cref="Call"/> over the page bridge. Games are a SQLite
    /// file on OPFS (/opfs/data.db, mounted by host-worker.js): every commit is written there.
    /// Runs until the tab closes.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class HostPage
    {
        private const string DatabasePath = "/opfs/data.db";

        /// <summary>nordvik-webrtc.js's stand-in in the worker (relative to the runtime's _framework/).</summary>
        public const string WorkerRtcModule = "../nordvik-webrtc-worker.js";

        private static HostGm? _gm;

        [JSImport("ready", "host-bridge")]
        private static partial void Ready(string centralUrl);

        public static async Task RunAsync(string central)
        {
            await JsBrowserRtc.InitializeAsync(WorkerRtcModule);
            await JSHost.ImportAsync("host-bridge", "../worker-bridge.js");

            var provider = WasmHost.Build(WasmHost.Configuration(central, databasePath: DatabasePath), new JsBrowserRtc());
            _gm = provider.GetRequiredService<HostGm>();
            Ready(central);
            Console.WriteLine($"GM host ready (Central {central}, games on OPFS).");

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
    }
}

using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.RealSession
{
    /// <summary>
    /// Entry-point fork between the harness suite (index.html/main.js, unchanged) and
    /// the real GM session (gm.html, sets window.__nordvikMode = 'gm' before main.js
    /// runs). Same JSHost.ImportAsync + [JSImport] pattern already used by
    /// TestSignalingService for testSignaling.js.
    /// </summary>
    internal static partial class HostMode
    {
        public static async Task<bool> IsRealSessionAsync()
        {
            await JSHost.ImportAsync("host-mode", "/hostMode.js");
            return GetMode() == "gm";
        }

        [JSImport("getMode", "host-mode")]
        private static partial string GetMode();
    }
}

#nullable enable
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NordvikManager.Hosting
{
    /// <summary>
    /// Opens the GM's browser on the server once it is running. Off with --no-browser or
    /// "OpenBrowser": false in appsettings.json, and whenever there is no one at a console.
    /// </summary>
    public static class BrowserLauncher
    {
        public const string NoBrowserFlag = "--no-browser";

        /// <summary>The flag isn't a configuration key, so it's taken out before the host parses the arguments.</summary>
        public static (string[] Remaining, bool NoBrowser) SplitArgs(string[] args) =>
            (args.Where(a => !string.Equals(a, NoBrowserFlag, StringComparison.OrdinalIgnoreCase)).ToArray(),
             args.Any(a => string.Equals(a, NoBrowserFlag, StringComparison.OrdinalIgnoreCase)));

        public static bool ShouldOpen(bool noBrowserFlag, IConfiguration configuration, bool isDevelopment, bool interactive) =>
            !noBrowserFlag
            && configuration.GetValue("OpenBrowser", true)
            && !isDevelopment
            && interactive;

        /// <summary>Best effort: a missing browser or desktop only means the GM opens the address by hand.</summary>
        public static bool TryOpen(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", url);
                else if (Environment.GetEnvironmentVariable("DISPLAY") != null || Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null)
                    Process.Start("xdg-open", url);
                else
                    return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

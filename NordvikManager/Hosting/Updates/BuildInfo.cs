#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>What this server is: version, build time (release builds) and platform.</summary>
    public static class BuildInfo
    {
        public static string Version
        {
            get
            {
                var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                // The SDK appends "+<commit>" to the informational version.
                var plus = version.IndexOf('+');
                return plus < 0 ? version : version[..plus];
            }
        }

        /// <summary>Set by release builds (the csproj, with a RuntimeIdentifier); orders dev builds of one version.</summary>
        public static DateTimeOffset? BuiltAt
        {
            get
            {
                var value = Assembly.GetEntryAssembly()?.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value;
                return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var built) ? built : null;
            }
        }

        public static CurrentBuild Current => new(Version, BuiltAt, RuntimeInformation.RuntimeIdentifier);

        /// <summary>
        /// Only the released server (the single NordvikManager executable, not `dotnet run`) can swap
        /// its own files; isDevelopment covers running from source with a release-like name.
        /// </summary>
        public static UpdateEnvironment UpdateEnvironment(IConfiguration configuration, bool isDevelopment)
        {
            var appFolder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            var centralUrl = configuration["CentralServerUrl"];
            var reason =
                isDevelopment ? "Updates are off in Development."
                : !string.Equals(Path.GetFileName(Environment.ProcessPath), UpdateInstaller.ExecutableName, StringComparison.OrdinalIgnoreCase)
                    ? "Only the released NordvikManager server can update itself; this one runs from source."
                : string.IsNullOrWhiteSpace(centralUrl) ? "No Central Server is set (CentralServerUrl in appsettings.json), so updates can't be found."
                : null;
            // For testing the updater against a local fake of Central and GitHub. Whoever can set the
            // server's environment can replace its exe anyway, so this opens nothing new.
            var downloadPrefix = Environment.GetEnvironmentVariable("NORDVIK_UPDATE_DOWNLOAD_PREFIX") is { Length: > 0 } prefix
                ? prefix
                : UpdateChecker.ReleaseDownloadPrefix;
            return new UpdateEnvironment(appFolder, Path.Combine(appFolder, "data", "updates"), centralUrl, Current, reason, downloadPrefix, UpdateStartup.UnderSystemd);
        }
    }
}

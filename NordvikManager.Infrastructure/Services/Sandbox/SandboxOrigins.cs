using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace NordvikManager.Infrastructure.Services.Sandbox
{
    /// <summary>
    /// The hosts addon cards and Html chat messages may load from: Security:SandboxAllowedOrigins
    /// from appsettings (defaults: the GitHub and Google Fonts hosts the bundled addons and
    /// imported Roll20 sheets use), plus the ones the local admin saved in Application Settings
    /// (server-settings.json). The backend enforces it on its own sandbox page
    /// (SecurityHeadersMiddleWare); clients get it from api/security/sandboxorigins and enforce
    /// it on the player side, whose pages come from the Central Server.
    /// </summary>
    public class SandboxOrigins
    {
        public const string ConfigKey = "Security:SandboxAllowedOrigins";

        public static readonly string[] Defaults =
        {
            "https://raw.githubusercontent.com",
            "https://api.github.com",
            "https://fonts.googleapis.com",
            "https://fonts.gstatic.com",
        };

        // https://host or https://*.host, optional port, nothing else — keeps CSP keywords,
        // extra directives and quotes out of the policies built from this list. \z, not $:
        // $ also matches before a trailing newline, which would end up in the header.
        private static readonly Regex OriginPattern = new(@"^https://(\*\.)?[a-z0-9-]+(\.[a-z0-9-]+)*(:\d{1,5})?\z", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>From appsettings, or the defaults when it lists none; Application Settings can't remove these.</summary>
        public IReadOnlyList<string> Configured { get; }

        private volatile IReadOnlyList<string> runtime = Array.Empty<string>();
        private volatile IReadOnlyList<string> origins;

        /// <summary>The ones saved from Application Settings.</summary>
        public IReadOnlyList<string> RuntimeEntries => runtime;

        /// <summary>All allowed origins. Replaced as a whole on every save, so callers can cache by reference.</summary>
        public IReadOnlyList<string> Origins => origins;

        public SandboxOrigins(IConfiguration configuration, ILogger<SandboxOrigins> logger)
        {
            var configured = configuration.GetSection(ConfigKey).Get<string[]>();
            var (valid, ignored) = Parse(configured is { Length: > 0 } ? configured : Defaults);
            foreach (var origin in ignored)
                logger.LogWarning(ConfigKey + ": ignoring \"{Origin}\" — use an https origin such as https://cdn.example.com", origin);
            Configured = valid;
            origins = valid;
        }

        /// <summary>Replaces the origins saved from Application Settings; returns the entries it skipped.</summary>
        public IReadOnlyList<string> SetRuntimeOrigins(IEnumerable<string?> entries)
        {
            var (valid, ignored) = Parse(entries);
            runtime = valid;
            origins = Configured.Concat(valid).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return ignored;
        }

        /// <summary>The entries that aren't an https origin (an empty result: all valid).</summary>
        public static IReadOnlyList<string> Invalid(IEnumerable<string?> entries) => Parse(entries).Ignored;

        private static (List<string> Valid, List<string> Ignored) Parse(IEnumerable<string?> entries)
        {
            var valid = new List<string>();
            var ignored = new List<string>();
            foreach (var origin in entries)
            {
                var trimmed = origin?.Trim() ?? string.Empty;
                if (OriginPattern.IsMatch(trimmed))
                    valid.Add(trimmed);
                else
                    ignored.Add(origin ?? string.Empty);
            }
            return (valid, ignored);
        }
    }
}

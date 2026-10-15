using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DNDOnePlaceManager.Engine
{
    /// <summary>
    /// The GM's Security:SandboxAllowedOrigins: the hosts addon cards and Html chat
    /// messages may load from (defaults: the GitHub and Google Fonts hosts the bundled
    /// addons and imported Roll20 sheets use). The backend enforces it on its own
    /// sandbox page (SecurityHeadersMiddleWare); clients get it from
    /// api/security/sandboxorigins and enforce it on the player side, whose pages come
    /// from the Central Server.
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
        // extra directives and quotes out of the policies built from this list.
        private static readonly Regex OriginPattern = new(@"^https://(\*\.)?[a-z0-9-]+(\.[a-z0-9-]+)*(:\d{1,5})?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IReadOnlyList<string> Origins { get; }

        public SandboxOrigins(IConfiguration configuration, ILogger<SandboxOrigins> logger)
        {
            var configured = configuration.GetSection(ConfigKey).Get<string[]>();
            var origins = configured is { Length: > 0 } ? configured : Defaults;

            var valid = new List<string>();
            foreach (var origin in origins)
            {
                var trimmed = origin?.Trim() ?? string.Empty;
                if (OriginPattern.IsMatch(trimmed))
                    valid.Add(trimmed);
                else
                    logger.LogWarning(ConfigKey + ": ignoring \"{Origin}\" — use an https origin such as https://cdn.example.com", origin);
            }
            Origins = valid;
        }
    }
}

using System.Reflection;

namespace NordvikManager.Api
{
    /// <summary>The kinds of GM host (<see cref="IHostDescription.Kind"/>).</summary>
    public static class HostKinds
    {
        /// <summary>The native GM server (the program a GM runs).</summary>
        public const string Server = "server";

        /// <summary>A GM host running in a browser tab (WebAssembly).</summary>
        public const string Browser = "browser";
    }

    /// <summary>
    /// What a GM host is, registered by each host: GET api/Host/Info (HostEndpoints) answers from
    /// it. The frontend shows it to the GM, and it's the source of the docs' list of what a browser
    /// host doesn't provide and of Central's warning to GMs.
    /// </summary>
    public interface IHostDescription
    {
        /// <summary>One of <see cref="HostKinds"/>.</summary>
        string Kind { get; }

        /// <summary>The host's build version, if it has one.</summary>
        string? Version { get; }

        /// <summary>
        /// Whether this host serves the server-only routes (<see cref="ApiNativeOnly"/> and
        /// [ApiRoute(NativeOnly = true)]); without them, they're listed as unavailable.
        /// </summary>
        bool HasServerFeatures { get; }

        /// <summary>What else a GM should know about this host (beyond the unavailable routes).</summary>
        IReadOnlyList<HostLimitation> Limitations { get; }
    }

    /// <summary>Something a GM should know about a host: a stable id, a title and the details.</summary>
    public sealed record HostLimitation(string Id, string Title, string Detail);

    /// <summary>A route (or a whole controller's, e.g. "api/Backup") this host doesn't serve, and why.</summary>
    public sealed record HostUnavailableFeature(string Path, string Reason);

    /// <summary>GET api/Host/Info.</summary>
    public sealed record HostInfoResponse(
        string Kind,
        string? Version,
        int Protocol,
        IReadOnlyList<HostUnavailableFeature> Unavailable,
        IReadOnlyList<HostLimitation> Limitations);

    public static class HostVersion
    {
        /// <summary>The entry assembly's informational version, without the SDK's "+commit".</summary>
        public static string? OfEntryAssembly()
        {
            var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(version))
                return null;
            var plus = version.IndexOf('+');
            return plus < 0 ? version : version[..plus];
        }
    }
}

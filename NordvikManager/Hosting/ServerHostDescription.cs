#nullable enable
using System;
using System.Collections.Generic;
using NordvikManager.Api;
using NordvikManager.Hosting.Updates;

namespace NordvikManager.Hosting
{
    /// <summary>The native GM server, for GET api/Host/Info: every feature, nothing to warn about.</summary>
    public sealed class ServerHostDescription : IHostDescription
    {
        public string Kind => HostKinds.Server;

        public string? Version => string.IsNullOrEmpty(BuildInfo.Version) ? null : BuildInfo.Version;

        public bool HasServerFeatures => true;

        public IReadOnlyList<HostLimitation> Limitations => Array.Empty<HostLimitation>();
    }
}

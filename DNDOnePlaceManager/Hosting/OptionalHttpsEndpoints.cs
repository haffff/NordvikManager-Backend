using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DNDOnePlaceManager.Hosting
{
    /// <summary>
    /// Makes HTTPS optional. Kestrel refuses to start when an HTTPS endpoint has no certificate,
    /// so HTTPS endpoints without one (per-endpoint, Certificates:Default, or the ASP.NET developer
    /// certificate) are removed from the Kestrel section and the server runs over HTTP only.
    /// </summary>
    public static class OptionalHttpsEndpoints
    {
        // Extension Kestrel uses to recognise the ASP.NET Core developer certificate.
        private const string DeveloperCertificateOid = "1.3.6.1.4.1.311.84.1.1";

        public static IConfiguration Filter(IConfigurationSection kestrel, bool developerCertificateAvailable, ICollection<string> dropped)
        {
            var values = kestrel.AsEnumerable(makePathsRelative: true)
                .Where(kv => kv.Value != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            var hasDefaultCertificate = kestrel.GetSection("Certificates:Default").Exists();

            foreach (var endpoint in kestrel.GetSection("Endpoints").GetChildren())
            {
                var isHttps = endpoint["Url"]?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;
                var hasCertificate = endpoint.GetSection("Certificate").Exists() || endpoint.GetSection("Sni").Exists();

                if (!isHttps || hasCertificate || hasDefaultCertificate || developerCertificateAvailable)
                    continue;

                var prefix = $"Endpoints:{endpoint.Key}:";
                foreach (var key in values.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
                    values.Remove(key);
                dropped.Add(endpoint.Key);
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        public static bool IsDeveloperCertificateAvailable()
        {
            try
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var now = DateTime.Now;
                return store.Certificates.Any(c =>
                    c.HasPrivateKey
                    && c.NotBefore <= now && now <= c.NotAfter
                    && c.Extensions.Any(e => e.Oid?.Value == DeveloperCertificateOid));
            }
            catch
            {
                return false;
            }
        }
    }
}

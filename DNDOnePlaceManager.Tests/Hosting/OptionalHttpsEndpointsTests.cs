using DNDOnePlaceManager.Hosting;
using Microsoft.Extensions.Configuration;

namespace DNDOnePlaceManager.Tests.Hosting
{
    // A GM machine without a certificate used to crash at startup because the shipped
    // appsettings always declares an HTTPS endpoint. HTTPS is optional: endpoints without
    // a usable certificate are skipped and the server keeps running over HTTP.
    public class OptionalHttpsEndpointsTests
    {
        private static IConfigurationSection Kestrel(Dictionary<string, string?> values)
        {
            var root = new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(kv => "Kestrel:" + kv.Key, kv => kv.Value))
                .Build();
            return root.GetSection("Kestrel");
        }

        private static readonly Dictionary<string, string?> HttpAndHttps = new()
        {
            ["Endpoints:Http:Url"] = "http://localhost:8213",
            ["Endpoints:Https:Url"] = "https://localhost:8214",
        };

        [Fact]
        public void Filter_DropsHttpsEndpoint_WhenNoCertificateAvailable()
        {
            var dropped = new List<string>();

            var result = OptionalHttpsEndpoints.Filter(Kestrel(HttpAndHttps), developerCertificateAvailable: false, dropped);

            Assert.Equal("http://localhost:8213", result["Endpoints:Http:Url"]);
            Assert.False(result.GetSection("Endpoints:Https").Exists());
            Assert.Equal(new[] { "Https" }, dropped);
        }

        [Fact]
        public void Filter_KeepsHttpsEndpoint_WhenDeveloperCertificateAvailable()
        {
            var dropped = new List<string>();

            var result = OptionalHttpsEndpoints.Filter(Kestrel(HttpAndHttps), developerCertificateAvailable: true, dropped);

            Assert.Equal("https://localhost:8214", result["Endpoints:Https:Url"]);
            Assert.Empty(dropped);
        }

        [Fact]
        public void Filter_KeepsHttpsEndpoint_WhenEndpointHasCertificate()
        {
            var values = new Dictionary<string, string?>(HttpAndHttps)
            {
                ["Endpoints:Https:Certificate:Path"] = "cert.pfx",
                ["Endpoints:Https:Certificate:Password"] = "secret",
            };
            var dropped = new List<string>();

            var result = OptionalHttpsEndpoints.Filter(Kestrel(values), developerCertificateAvailable: false, dropped);

            Assert.Equal("https://localhost:8214", result["Endpoints:Https:Url"]);
            Assert.Equal("cert.pfx", result["Endpoints:Https:Certificate:Path"]);
            Assert.Empty(dropped);
        }

        [Fact]
        public void Filter_KeepsHttpsEndpoint_WhenDefaultCertificateConfigured()
        {
            var values = new Dictionary<string, string?>(HttpAndHttps)
            {
                ["Certificates:Default:Path"] = "cert.pfx",
            };
            var dropped = new List<string>();

            var result = OptionalHttpsEndpoints.Filter(Kestrel(values), developerCertificateAvailable: false, dropped);

            Assert.Equal("https://localhost:8214", result["Endpoints:Https:Url"]);
            Assert.Equal("cert.pfx", result["Certificates:Default:Path"]);
            Assert.Empty(dropped);
        }

        [Fact]
        public void Filter_DetectsHttpsByUrlScheme_NotByEndpointName()
        {
            var values = new Dictionary<string, string?>
            {
                ["Endpoints:Secure:Url"] = "HTTPS://*:8081",
                ["Endpoints:Https:Url"] = "http://*:8080",
            };
            var dropped = new List<string>();

            var result = OptionalHttpsEndpoints.Filter(Kestrel(values), developerCertificateAvailable: false, dropped);

            Assert.False(result.GetSection("Endpoints:Secure").Exists());
            Assert.Equal("http://*:8080", result["Endpoints:Https:Url"]);
            Assert.Equal(new[] { "Secure" }, dropped);
        }

        [Fact]
        public void Filter_KeepsOtherKestrelSettings()
        {
            var values = new Dictionary<string, string?>(HttpAndHttps)
            {
                ["Limits:MaxRequestBodySize"] = "1000",
                ["EndpointDefaults:Protocols"] = "Http1",
            };

            var result = OptionalHttpsEndpoints.Filter(Kestrel(values), developerCertificateAvailable: false, new List<string>());

            Assert.Equal("1000", result["Limits:MaxRequestBodySize"]);
            Assert.Equal("Http1", result["EndpointDefaults:Protocols"]);
        }
    }
}

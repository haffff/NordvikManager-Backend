using NordvikManager.Api;
using NordvikManager.Api.Endpoints;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NordvikManager.Tests.Api
{
    // GET api/Host/Info: what kind of host the GM is on, and what it doesn't provide. One list,
    // for the frontend's host drawer, the docs report and Central's warning to GMs.
    public class HostEndpointsTests
    {
        private static readonly User Caller = new() { Id = Guid.NewGuid().ToString(), UserName = "gm" };

        private sealed class FakeHost : IHostDescription
        {
            public string Kind { get; init; } = HostKinds.Browser;
            public string? Version { get; init; } = "1.2.3";
            public bool HasServerFeatures { get; init; }
            public IReadOnlyList<HostLimitation> Limitations { get; init; } = Array.Empty<HostLimitation>();
        }

        private static HostInfoResponse Info(IHostDescription host)
        {
            var result = new HostEndpoints().Info(Caller, host);
            Assert.Equal(200, result.StatusCode);
            return Assert.IsType<HostInfoResponse>(result.Body);
        }

        [Fact]
        public void Info_Unauthorized_WithoutACaller()
        {
            Assert.Equal(401, new HostEndpoints().Info(null, new FakeHost()).StatusCode);
        }

        [Fact]
        public void Info_SaysTheHostsKindVersionAndProtocol()
        {
            var info = Info(new FakeHost { Kind = HostKinds.Browser, Version = "0.0.3" });

            Assert.Equal("browser", info.Kind);
            Assert.Equal("0.0.3", info.Version);
            Assert.Equal(ProtocolVersion.Current, info.Protocol);
        }

        [Fact]
        public void Info_ListsNothingUnavailable_OnAHostWithTheServerFeatures()
        {
            var info = Info(new FakeHost { Kind = HostKinds.Server, HasServerFeatures = true });

            Assert.Empty(info.Unavailable);
            Assert.Empty(info.Limitations);
        }

        [Fact]
        public void Info_ListsEveryServerOnlyControllerAndRoute_WithItsReason_OnAHostWithout()
        {
            var info = Info(new FakeHost { HasServerFeatures = false });

            var expected = ApiNativeOnly.Controllers.Select(c => ("api/" + c.Key, c.Value))
                .Concat(ApiRoutes.Shared.All.Where(r => r.NativeOnly).Select(r => (r.Path, r.Reason!)))
                .OrderBy(e => e.Item1, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.Equal(expected, info.Unavailable.Select(u => (u.Path, u.Reason)).ToList());
            Assert.Contains(info.Unavailable, u => u.Path == "api/Backup");
            Assert.Contains(info.Unavailable, u => u.Path == "api/Materials/LinkResource");
        }

        [Fact]
        public void Info_PassesOnTheHostsOwnLimitations()
        {
            var limitation = new HostLimitation("tab-open", "Keep this tab open", "The game runs in it.");

            var info = Info(new FakeHost { Limitations = new[] { limitation } });

            Assert.Equal(new[] { limitation }, info.Limitations);
        }
    }
}

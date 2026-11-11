using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    public class IceServerMapperTests
    {
        private static readonly List<IceServer> Fallback = new() { new IceServer("stun:fallback:19302") };

        private static IceServersResult Result(params IceServerDto[] servers) => new() { IceServers = servers.ToList(), Ttl = 86400 };

        [Fact]
        public void ToRtcIceServers_CreatesOneServerPerUrl()
        {
            var result = Result(new IceServerDto { Urls = new() { "stun:a:1", "stun:b:2" } });

            var servers = IceServerMapper.ToIceServers(result, Fallback);

            Assert.Equal(new[] { "stun:a:1", "stun:b:2" }, servers.Select(s => s.Url));
            Assert.All(servers, s => Assert.Null(s.Username));
        }

        [Fact]
        public void ToRtcIceServers_CarriesTurnCredentials()
        {
            var result = Result(
                new IceServerDto { Urls = new() { "stun:a:1" } },
                new IceServerDto { Urls = new() { "turn:t:3478?transport=udp" }, Username = "123:user", Credential = "secret=" });

            var turn = IceServerMapper.ToIceServers(result, Fallback).Single(s => s.Url.StartsWith("turn:"));

            Assert.Equal("123:user", turn.Username);
            Assert.Equal("secret=", turn.Credential);
        }

        [Theory]
        [InlineData("turns:t:5349?transport=tcp")]
        [InlineData("stuns:t:5349")]
        [InlineData("http://not-ice")]
        public void ToRtcIceServers_DropsUrlsTheBackendCannotUse(string url)
        {
            var result = Result(new IceServerDto { Urls = new() { "turn:t:3478?transport=udp", url }, Username = "u", Credential = "c" });

            var servers = IceServerMapper.ToIceServers(result, Fallback);

            Assert.Equal(new[] { "turn:t:3478?transport=udp" }, servers.Select(s => s.Url));
        }

        [Fact]
        public void ToRtcIceServers_PutsUdpTurnFirst()
        {
            // SIPSorcery ends up using a single ICE server and prefers TURN, so the first TURN URL must be the most reliable one.
            var result = Result(
                new IceServerDto { Urls = new() { "stun:a:1" } },
                new IceServerDto { Urls = new() { "turn:t:3478?transport=tcp", "turn:t:3478?transport=udp", "turn:t:3479" }, Username = "u", Credential = "c" });

            var servers = IceServerMapper.ToIceServers(result, Fallback);

            Assert.Equal(new[] { "turn:t:3478?transport=udp", "turn:t:3479", "turn:t:3478?transport=tcp", "stun:a:1" }, servers.Select(s => s.Url));
        }

        [Fact]
        public void ToRtcIceServers_UsesFallback_WhenNothingUsable()
        {
            Assert.Same(Fallback, IceServerMapper.ToIceServers(null, Fallback));
            Assert.Same(Fallback, IceServerMapper.ToIceServers(Result(), Fallback));
            Assert.Same(Fallback, IceServerMapper.ToIceServers(Result(new IceServerDto { Urls = new() { "turns:t:5349" } }), Fallback));
        }

        [Theory]
        [InlineData("{\"iceServers\":[{\"urls\":[\"stun:a:1\",\"stun:b:2\"]}],\"ttl\":null}")]
        [InlineData("{\"iceServers\":[{\"urls\":\"stun:a:1\"},{\"urls\":\"stun:b:2\"}],\"ttl\":null}")]
        public void IceServersResult_DeserializesUrlsAsStringOrArray(string json)
        {
            var result = JsonConvert.DeserializeObject<IceServersResult>(json)!;

            Assert.Equal(new[] { "stun:a:1", "stun:b:2" }, result.IceServers.SelectMany(s => s.Urls));
            Assert.Null(result.Ttl);
        }

        [Fact]
        public void MappedServers_AreAcceptedBySipsorcery()
        {
            var result = Result(
                new IceServerDto { Urls = new() { "stun:stun.l.google.com:19302" } },
                new IceServerDto
                {
                    Urls = new() { "turn:127.0.0.1:3478?transport=udp", "turn:127.0.0.1:3478?transport=tcp", "turns:127.0.0.1:5349?transport=tcp" },
                    Username = "123:user",
                    Credential = "secret=",
                });

            var pc = new SipsorceryPeerConnectionFactory().Create(IceServerMapper.ToIceServers(result, Fallback));
            pc.Close();
        }
    }
}

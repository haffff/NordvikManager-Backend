using Microsoft.Extensions.Configuration;
using NordvikManager.WebRTC;
using System.Text.Json;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    public class SignalingServiceTests
    {
        [Fact]
        public void BuildGmAuthenticatePayload_IncludesProtocolVersion_WhenAuthenticatingAsGm()
        {
            var payload = SignalingService.BuildGmAuthenticatePayload("token-1", "session-1");

            using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));
            var root = json.RootElement;
            Assert.Equal("token-1", root.GetProperty("token").GetString());
            Assert.Equal("session-1", root.GetProperty("sessionId").GetString());
            Assert.Equal("gm", root.GetProperty("role").GetString());
            Assert.Equal(ProtocolVersion.Current, root.GetProperty("protocol").GetInt32());
        }

        [Fact]
        public void ProtocolVersion_IsPositive()
        {
            Assert.True(ProtocolVersion.Current >= 1);
        }

        private static Microsoft.Extensions.Configuration.IConfiguration Config(string? transport) =>
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?> { ["Signaling:Transport"] = transport })
                .Build();

        // The native server keeps Socket.IO's default (long-polling, then upgrade).
        [Fact]
        public void Options_ByDefault_AreSocketIosOwn()
        {
            var defaults = new SocketIOClient.SocketIOOptions();

            var options = SignalingService.BuildOptions(Config(null));

            Assert.Equal(defaults.Transport, options.Transport);
            Assert.Equal(defaults.AutoUpgrade, options.AutoUpgrade);
        }

        // A browser-wasm host can't do the long-polling handshake through fetch(), so it
        // connects over WebSocket straight away.
        [Theory]
        [InlineData("WebSocket")]
        [InlineData("websocket")]
        public void Options_WebSocketTransport_ConnectsOverWebSocketOnly(string transport)
        {
            var options = SignalingService.BuildOptions(Config(transport));

            Assert.Equal(SocketIOClient.Common.TransportProtocol.WebSocket, options.Transport);
            Assert.False(options.AutoUpgrade);
        }
    }
}

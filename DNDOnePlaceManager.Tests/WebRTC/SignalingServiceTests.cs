using DNDOnePlaceManager.WebRTC;
using System.Text.Json;
using Xunit;

namespace DNDOnePlaceManager.Tests.WebRTC
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
    }
}

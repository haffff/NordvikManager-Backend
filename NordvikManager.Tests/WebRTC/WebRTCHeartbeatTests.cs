using Newtonsoft.Json.Linq;
using NordvikManager.WebRTC;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // The player client pings over the data channel to notice a dead channel; the backend
    // answers on the spot, without a trip through the HTTP pipeline.
    public class WebRTCHeartbeatTests
    {
        [Fact]
        public void ReplyFor_Heartbeat_IsAnOkApiResponseWithTheSameId()
        {
            var reply = WebRTCHeartbeat.ReplyFor(new WebRTCApiRequest { Id = "hb-1", Method = "GET", Path = WebRTCHeartbeat.Path });

            Assert.NotNull(reply);
            var json = JObject.Parse(reply!);
            Assert.Equal("api-response", json["type"]!.Value<string>());
            Assert.Equal("hb-1", json["id"]!.Value<string>());
            Assert.Equal(200, json["status"]!.Value<int>());
        }

        [Fact]
        public void ReplyFor_AnyOtherRequest_IsNull()
        {
            Assert.Null(WebRTCHeartbeat.ReplyFor(new WebRTCApiRequest { Id = "1", Method = "GET", Path = "/api/battlemap/getplayer" }));
            Assert.Null(WebRTCHeartbeat.ReplyFor(new WebRTCApiRequest { Id = "2", Method = "POST", Path = WebRTCHeartbeat.Path }));
        }
    }
}

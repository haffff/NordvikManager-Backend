using Newtonsoft.Json;
using SIPSorcery.Net;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Wraps a SIPSorcery RTCDataChannel as an IPlayerConnection so it can be used
    /// interchangeably with WebSocketManager in GameLobby.
    /// </summary>
    public class WebRTCPlayerConnection : IPlayerConnection
    {
        private const int ChunkSize = 15_000;

        private readonly RTCDataChannel _dataChannel;

        public WebRTCPlayerConnection(RTCDataChannel dataChannel)
        {
            _dataChannel = dataChannel;
        }

        public Task<bool> SendMessageToPlayer(object message)
        {
            if (_dataChannel.readyState != RTCDataChannelState.open)
                return Task.FromResult(false);

            var json = JsonConvert.SerializeObject(message);

            if (json.Length <= ChunkSize)
            {
                _dataChannel.send(json);
                return Task.FromResult(true);
            }

            var chunkId = Guid.NewGuid().ToString();
            var total = (int)Math.Ceiling((double)json.Length / ChunkSize);

            for (var i = 0; i < total; i++)
            {
                var offset = i * ChunkSize;
                var slice = json.Substring(offset, Math.Min(ChunkSize, json.Length - offset));
                _dataChannel.send(JsonConvert.SerializeObject(new
                {
                    type = "chunk",
                    chunkId,
                    index = i,
                    total,
                    data = slice
                }));
            }

            return Task.FromResult(true);
        }
    }
}

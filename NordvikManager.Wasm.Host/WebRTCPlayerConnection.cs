using DNDOnePlaceManager.WebRTC;
using Newtonsoft.Json;
using NordvikManager.Wasm.SipWrapper;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host
{
    /// <summary>
    /// Copied verbatim from DNDOnePlaceManager/WebRTC/WebRTCPlayerConnection.cs — the
    /// only change is the `using` swap (SIPSorcery.Net -> NordvikManager.Wasm.SipWrapper).
    /// Confirms the plan's claim that this file is a genuinely zero-touch port.
    /// IPlayerConnection now comes from DndOnePlaceManager.RealTime (the extracted
    /// real-time layer) rather than a local duplicate, so this satisfies the same
    /// interface GameLobby.cs expects.
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

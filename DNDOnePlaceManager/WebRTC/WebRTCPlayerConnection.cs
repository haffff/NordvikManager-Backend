using Newtonsoft.Json;
using SIPSorcery.Net;
using System;
using System.Collections.Generic;
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

        // Large messages are chunked and fed in only while the channel has less than this
        // buffered. SIPSorcery queues sends without limit, and the channel is ordered: pushing
        // a whole large message (e.g. a music track as base64) at once made every later
        // message to this player — token moves, chat — wait until all of it was sent.
        private const ulong BufferHigh = 1024 * 1024;

        private readonly IDataChannelSender _channel;

        // SIPSorcery has no "buffered amount low" event, so a waiting send checks back this often.
        private readonly TimeSpan _pollInterval;

        private readonly object _lock = new();
        private readonly Queue<OutgoingMessage> _outgoing = new();
        private bool _pumping;

        public WebRTCPlayerConnection(RTCDataChannel dataChannel)
            : this(new SipsorceryDataChannelSender(dataChannel))
        {
        }

        public WebRTCPlayerConnection(IDataChannelSender channel, TimeSpan? pollInterval = null)
        {
            _channel = channel;
            _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(10);
        }

        /// <summary>
        /// Small messages are sent at once, also while a large one is waiting. Large ones
        /// are sent one after another, as the buffer drains; the task completes once all of
        /// it is handed to the channel. False if the channel is or gets closed first.
        /// </summary>
        public Task<bool> SendMessageToPlayer(object message)
        {
            if (!_channel.IsOpen)
                return Task.FromResult(false);

            var json = JsonConvert.SerializeObject(message);

            if (json.Length <= ChunkSize)
            {
                try
                {
                    _channel.Send(json);
                    return Task.FromResult(true);
                }
                catch (Exception)
                {
                    return Task.FromResult(false);
                }
            }

            var outgoing = new OutgoingMessage(json);
            bool startPump;
            lock (_lock)
            {
                _outgoing.Enqueue(outgoing);
                startPump = !_pumping;
                _pumping = true;
            }
            if (startPump)
                _ = Task.Run(PumpAsync);

            return outgoing.Completion.Task;
        }

        private async Task PumpAsync()
        {
            while (true)
            {
                OutgoingMessage outgoing;
                lock (_lock)
                {
                    if (_outgoing.Count == 0)
                    {
                        _pumping = false;
                        return;
                    }
                    outgoing = _outgoing.Peek();
                }

                var sent = await SendChunksAsync(outgoing.Json);

                lock (_lock)
                    _outgoing.Dequeue();
                outgoing.Completion.TrySetResult(sent);

                if (!sent && !_channel.IsOpen)
                {
                    FailQueued();
                    return;
                }
            }
        }

        private async Task<bool> SendChunksAsync(string json)
        {
            var chunkId = Guid.NewGuid().ToString();
            var total = (int)Math.Ceiling((double)json.Length / ChunkSize);

            for (var i = 0; i < total; i++)
            {
                while (_channel.IsOpen && _channel.BufferedAmount >= BufferHigh)
                    await Task.Delay(_pollInterval);
                if (!_channel.IsOpen)
                    return false;

                var offset = i * ChunkSize;
                var slice = json.Substring(offset, Math.Min(ChunkSize, json.Length - offset));
                try
                {
                    _channel.Send(JsonConvert.SerializeObject(new
                    {
                        type = "chunk",
                        chunkId,
                        index = i,
                        total,
                        data = slice
                    }));
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        private void FailQueued()
        {
            List<OutgoingMessage> failed;
            lock (_lock)
            {
                failed = new List<OutgoingMessage>(_outgoing);
                _outgoing.Clear();
                _pumping = false;
            }
            foreach (var outgoing in failed)
                outgoing.Completion.TrySetResult(false);
        }

        private sealed class OutgoingMessage
        {
            public OutgoingMessage(string json) => Json = json;

            public string Json { get; }

            public TaskCompletionSource<bool> Completion { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

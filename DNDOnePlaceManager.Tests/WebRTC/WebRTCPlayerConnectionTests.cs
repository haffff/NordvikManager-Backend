using DNDOnePlaceManager.WebRTC;
using Newtonsoft.Json.Linq;
using System.Text;

namespace DNDOnePlaceManager.Tests.WebRTC
{
    // SendMessageToPlayer used to push every chunk of a large message into SIPSorcery's
    // (unbounded) send queue at once. On an ordered channel, every later message to that
    // player — token moves, chat — then waited behind the whole thing (e.g. a ~23 MB music
    // track as base64). Large messages are now fed in as the buffer drains.
    public class WebRTCPlayerConnectionTests
    {
        private const int MB = 1024 * 1024;

        /// <summary>A data channel whose buffer only drains when the test says so.</summary>
        private class FakeChannel : IDataChannelSender
        {
            private readonly object _lock = new();
            private ulong _buffered;
            public List<string> Sent { get; } = new();
            public bool IsOpen { get; set; } = true;
            public ulong MaxBuffered { get; private set; }

            public ulong BufferedAmount { get { lock (_lock) return _buffered; } }

            public void Send(string message)
            {
                lock (_lock)
                {
                    Sent.Add(message);
                    _buffered += (ulong)Encoding.UTF8.GetByteCount(message);
                    MaxBuffered = Math.Max(MaxBuffered, _buffered);
                }
            }

            public void Drain() { lock (_lock) _buffered = 0; }

            public int Count { get { lock (_lock) return Sent.Count; } }
            public List<string> Snapshot() { lock (_lock) return Sent.ToList(); }
        }

        private static WebRTCPlayerConnection Connection(FakeChannel channel) =>
            new(channel, pollInterval: TimeSpan.FromMilliseconds(1));

        private static object Big(int bytes, string tag = "a") => new { type = "api-response", tag, body = new string('x', bytes) };

        /// <summary>Drains the fake channel until the task finishes (or gives up).</summary>
        private static async Task<T> DrainUntil<T>(FakeChannel channel, Task<T> task)
        {
            for (int i = 0; i < 2000 && !task.IsCompleted; i++)
            {
                await Task.Delay(1);
                channel.Drain();
            }
            return await task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private static string Reassemble(IEnumerable<string> sent)
        {
            var chunks = sent.Select(JObject.Parse).Where(c => c["type"]?.Value<string>() == "chunk").ToList();
            Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c["index"]!.Value<int>()));
            Assert.All(chunks, c => Assert.Equal(chunks.Count, c["total"]!.Value<int>()));
            return string.Concat(chunks.Select(c => c["data"]!.Value<string>()));
        }

        [Fact]
        public async Task SendMessageToPlayer_SmallMessage_SentAtOnce()
        {
            var channel = new FakeChannel();

            Assert.True(await Connection(channel).SendMessageToPlayer(new { command = "element_move" }));

            Assert.Single(channel.Sent);
        }

        [Fact]
        public async Task SendMessageToPlayer_ClosedChannel_ReturnsFalse()
        {
            var channel = new FakeChannel { IsOpen = false };

            Assert.False(await Connection(channel).SendMessageToPlayer(new { command = "x" }));
            Assert.Empty(channel.Sent);
        }

        [Fact]
        public async Task SendMessageToPlayer_LargeMessage_KeepsAbout1MBBuffered_AndArrivesWhole()
        {
            var channel = new FakeChannel();
            var message = Big(5 * MB);

            var sending = Connection(channel).SendMessageToPlayer(message);
            await Task.Delay(50);

            Assert.False(sending.IsCompleted); // waiting for the buffer to drain
            Assert.True(channel.MaxBuffered <= 1.1 * MB, $"buffered {channel.MaxBuffered}");

            Assert.True(await DrainUntil(channel, sending));
            Assert.True(channel.MaxBuffered <= 1.1 * MB, $"buffered {channel.MaxBuffered}");
            Assert.Equal(Newtonsoft.Json.JsonConvert.SerializeObject(message), Reassemble(channel.Snapshot()));
        }

        [Fact]
        public async Task SendMessageToPlayer_SmallMessageWhileLargeWaits_GoesOutAtOnce()
        {
            var channel = new FakeChannel();
            var connection = Connection(channel);
            var sending = connection.SendMessageToPlayer(Big(5 * MB));
            await Task.Delay(50);
            var before = channel.Count;

            Assert.True(await connection.SendMessageToPlayer(new { command = "element_move" }));

            Assert.Equal(before + 1, channel.Count);
            Assert.Contains("element_move", channel.Snapshot().Last());
            await DrainUntil(channel, sending);
        }

        [Fact]
        public async Task SendMessageToPlayer_TwoLargeMessages_SentOneAfterTheOther()
        {
            var channel = new FakeChannel();
            var connection = Connection(channel);

            var first = connection.SendMessageToPlayer(Big(3 * MB, "first"));
            var second = connection.SendMessageToPlayer(Big(3 * MB, "second"));
            await Task.Delay(50);
            Assert.True(channel.MaxBuffered <= 1.1 * MB, $"buffered {channel.MaxBuffered}");

            Assert.True(await DrainUntil(channel, first));
            Assert.True(await DrainUntil(channel, second));

            // Not interleaved: all of the first message's chunks come before the second's.
            var chunkIds = channel.Snapshot().Select(s => JObject.Parse(s)["chunkId"]!.Value<string>()).ToList();
            var firstId = chunkIds[0];
            Assert.Equal(chunkIds.Count(x => x == firstId), chunkIds.TakeWhile(x => x == firstId).Count());
        }

        [Fact]
        public async Task SendMessageToPlayer_ChannelClosesWhileWaiting_ReturnsFalse()
        {
            var channel = new FakeChannel();
            var connection = Connection(channel);
            var first = connection.SendMessageToPlayer(Big(5 * MB));
            var second = connection.SendMessageToPlayer(Big(2 * MB));
            await Task.Delay(50);

            channel.IsOpen = false;
            channel.Drain();

            Assert.False(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(await second.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }
}

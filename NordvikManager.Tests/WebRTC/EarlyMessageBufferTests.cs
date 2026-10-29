using NordvikManager.WebRTC;
using System.Text;

namespace NordvikManager.Tests.WebRTC
{
    // A player's client sends as soon as its data channel opens, while the backend is still
    // resolving who the player is (GetPlayer/AddPlayer). Those messages used to be dropped and
    // their requests never answered; they're now held and handled once the player is known.
    public class EarlyMessageBufferTests
    {
        private static byte[] Msg(string text) => Encoding.UTF8.GetBytes(text);
        private static string Text(byte[] data) => Encoding.UTF8.GetString(data);

        [Fact]
        public void Receive_HoldsMessages_BeforeOpen()
        {
            var buffer = new EarlyMessageBuffer();

            Assert.Equal(EarlyMessageBuffer.Route.Held, buffer.Receive(Msg("a")));
            Assert.Equal(EarlyMessageBuffer.Route.Held, buffer.Receive(Msg("b")));
        }

        [Fact]
        public void Open_ReplaysHeldMessagesInOrder_ThenLetsNewOnesThrough()
        {
            var buffer = new EarlyMessageBuffer();
            buffer.Receive(Msg("api/session/whoami"));
            buffer.Receive(Msg("api/battlemap/getplayer"));

            var handled = new List<string>();
            buffer.Open(data => handled.Add(Text(data)));

            Assert.Equal(new[] { "api/session/whoami", "api/battlemap/getplayer" }, handled);
            Assert.Equal(EarlyMessageBuffer.Route.Handle, buffer.Receive(Msg("later")));
        }

        [Fact]
        public void Open_WithNothingHeld_HandlesNothing()
        {
            var buffer = new EarlyMessageBuffer();
            var handled = 0;
            buffer.Open(_ => handled++);
            Assert.Equal(0, handled);
        }

        [Fact]
        public async Task Receive_DuringReplay_WaitsAndComesAfterTheHeldMessages()
        {
            var buffer = new EarlyMessageBuffer();
            buffer.Receive(Msg("first"));

            var handled = new List<string>();
            var replaying = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var open = Task.Run(() => buffer.Open(data =>
            {
                replaying.Set();
                release.Wait(TimeSpan.FromSeconds(5));
                lock (handled) handled.Add(Text(data));
            }));

            replaying.Wait(TimeSpan.FromSeconds(5));
            var late = Task.Run(() =>
            {
                // Arrives while "first" is being replayed: it must not overtake it.
                if (buffer.Receive(Msg("second")) == EarlyMessageBuffer.Route.Handle)
                    lock (handled) handled.Add("second");
            });
            await Task.Delay(50);
            release.Set();
            await Task.WhenAll(open, late);

            Assert.Equal(new[] { "first", "second" }, handled);
        }

        [Fact]
        public void Fail_GivesBackTheHeldMessages_AndDropsLaterOnes()
        {
            var buffer = new EarlyMessageBuffer();
            buffer.Receive(Msg("a"));
            buffer.Receive(Msg("b"));

            var held = buffer.Fail();

            Assert.Equal(new[] { "a", "b" }, held.Select(Text));
            Assert.Equal(EarlyMessageBuffer.Route.Dropped, buffer.Receive(Msg("c")));
        }

        [Fact]
        public void Receive_DropsMessages_OverTheLimit()
        {
            var buffer = new EarlyMessageBuffer(capacity: 2);
            buffer.Receive(Msg("a"));
            buffer.Receive(Msg("b"));

            Assert.Equal(EarlyMessageBuffer.Route.Dropped, buffer.Receive(Msg("c")));

            var handled = new List<string>();
            buffer.Open(data => handled.Add(Text(data)));
            Assert.Equal(new[] { "a", "b" }, handled);
        }
    }
}

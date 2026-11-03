using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // After its signaling socket drops, the GM backend keeps reconnecting (backing off)
    // until it is back, Central refuses it, or the session is stopped.
    public class SignalingReconnectTests
    {
        private readonly List<TimeSpan> _waits = new();
        private Task Wait(TimeSpan t) { _waits.Add(t); return Task.CompletedTask; }

        [Fact]
        public async Task RetriesUntilConnected_BackingOff()
        {
            var attempts = 0;
            var result = await SignalingReconnect.RunAsync(
                () => ++attempts < 4 ? throw new Exception("Central unreachable") : Task.CompletedTask,
                () => true,
                Wait);

            Assert.Equal(SignalingReconnect.Outcome.Connected, result);
            Assert.Equal(4, attempts);
            Assert.Equal(4, _waits.Count);
            for (var i = 1; i < _waits.Count; i++)
                Assert.True(_waits[i] >= _waits[i - 1]);
        }

        [Fact]
        public async Task StopsWhenCentralRefusesIt()
        {
            var attempts = 0;
            var result = await SignalingReconnect.RunAsync(
                () => { attempts++; throw new SignalingAuthException("This game was opened as GM from another connection"); },
                () => true,
                Wait);

            Assert.Equal(SignalingReconnect.Outcome.Refused, result);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task StopsWhenTheSessionNoLongerWantsIt()
        {
            var attempts = 0;
            var keepGoing = true;
            var result = await SignalingReconnect.RunAsync(
                () => { attempts++; keepGoing = false; throw new Exception("down"); },
                () => keepGoing,
                Wait);

            Assert.Equal(SignalingReconnect.Outcome.Stopped, result);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public void DelayGrowsAndIsCapped()
        {
            Assert.Equal(TimeSpan.FromSeconds(1), SignalingReconnect.DelayFor(0));
            Assert.True(SignalingReconnect.DelayFor(3) > SignalingReconnect.DelayFor(1));
            Assert.Equal(SignalingReconnect.DelayFor(50), SignalingReconnect.DelayFor(500));
            Assert.True(SignalingReconnect.DelayFor(500) <= TimeSpan.FromSeconds(30));
        }
    }
}

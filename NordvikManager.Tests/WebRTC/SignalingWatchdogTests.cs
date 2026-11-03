using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // SocketIOClient doesn't notice a connection that died silently (a Wi-Fi switch leaves it
    // half open), so the GM backend stayed "connected" to signaling while Central had long
    // dropped it: no new players, no reconnects, until the backend restarted. The watchdog
    // asks Central now and then; no answer, or "you aren't the GM here", ends the connection
    // (and the reconnect loop takes over).
    public class SignalingWatchdogTests
    {
        private readonly List<TimeSpan> _waits = new();
        private Task Wait(TimeSpan t) { _waits.Add(t); return Task.CompletedTask; }

        private static Func<Task<SignalingWatchdog.Answer>> Answers(params SignalingWatchdog.Answer[] answers)
        {
            var i = 0;
            return () => Task.FromResult(i < answers.Length ? answers[i++] : SignalingWatchdog.Answer.None);
        }

        [Fact]
        public async Task NoAnswerAfterAnswering_IsDead()
        {
            var dead = false;
            var keep = 10;
            await SignalingWatchdog.RunAsync(
                Answers(SignalingWatchdog.Answer.Gm, SignalingWatchdog.Answer.Gm, SignalingWatchdog.Answer.None),
                () => keep-- > 0,
                () => { dead = true; return Task.CompletedTask; },
                Wait);

            Assert.True(dead);
        }

        [Fact]
        public async Task CentralSayingItIsNotTheGm_IsDead()
        {
            var dead = false;
            await SignalingWatchdog.RunAsync(
                Answers(SignalingWatchdog.Answer.Gm, SignalingWatchdog.Answer.NotGm),
                () => true,
                () => { dead = true; return Task.CompletedTask; },
                Wait);

            Assert.True(dead);
        }

        [Fact]
        public async Task ACentralThatNeverAnswered_IsLeftAlone()
        {
            // An older Central without the handler: nothing to measure by.
            var dead = false;
            var keep = 10;
            await SignalingWatchdog.RunAsync(
                Answers(),
                () => keep-- > 0,
                () => { dead = true; return Task.CompletedTask; },
                Wait);

            Assert.False(dead);
        }

        [Fact]
        public async Task StopsWhenTheConnectionIsNoLongerCurrent()
        {
            var asked = 0;
            await SignalingWatchdog.RunAsync(
                () => { asked++; return Task.FromResult(SignalingWatchdog.Answer.Gm); },
                () => asked < 3,
                () => Task.CompletedTask,
                Wait);

            Assert.Equal(3, asked);
        }
    }
}

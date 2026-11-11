using NordvikManager.WebRTC;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // 'disconnected' is a pause ICE often recovers from (a Wi-Fi blip, a laptop lid);
    // only a peer that stays disconnected past the grace period, fails or closes is let go.
    public class PeerStateWatcherTests
    {
        private int _deadCount;
        private TaskCompletionSource? _graceElapsed;
        private CancellationToken _graceToken;

        private PeerStateWatcher Create() => new(
            TimeSpan.FromSeconds(10),
            () => Interlocked.Increment(ref _deadCount),
            (_, token) =>
            {
                _graceToken = token;
                _graceElapsed = new TaskCompletionSource();
                return _graceElapsed.Task;
            });

        [Theory]
        [InlineData(PeerState.Failed)]
        [InlineData(PeerState.Closed)]
        public void FailedOrClosed_IsDeadAtOnce(PeerState state)
        {
            var watcher = Create();
            watcher.OnStateChanged(state);
            Assert.Equal(1, _deadCount);
        }

        [Fact]
        public void Disconnected_IsNotDeadYet()
        {
            var watcher = Create();
            watcher.OnStateChanged(PeerState.Disconnected);
            Assert.Equal(0, _deadCount);
        }

        [Fact]
        public async Task Disconnected_PastTheGracePeriod_IsDead()
        {
            var watcher = Create();
            watcher.OnStateChanged(PeerState.Disconnected);

            _graceElapsed!.SetResult();
            await Task.Yield();

            Assert.Equal(1, _deadCount);
        }

        [Fact]
        public async Task Disconnected_ThenConnectedAgain_StaysAlive()
        {
            var watcher = Create();
            watcher.OnStateChanged(PeerState.Disconnected);
            watcher.OnStateChanged(PeerState.Connected);

            Assert.True(_graceToken.IsCancellationRequested);
            _graceElapsed!.TrySetResult();
            await Task.Yield();

            Assert.Equal(0, _deadCount);
        }

        [Fact]
        public void IsDeadOnlyOnce()
        {
            var watcher = Create();
            watcher.OnStateChanged(PeerState.Failed);
            watcher.OnStateChanged(PeerState.Closed);
            Assert.Equal(1, _deadCount);
        }

        [Theory]
        [InlineData(PeerState.New, true)]
        [InlineData(PeerState.Connecting, true)]
        [InlineData(PeerState.Connected, false)]
        [InlineData(PeerState.Disconnected, false)]
        public void SignalingLeave_ClosesOnlyAPeerThatIsStillNegotiating(PeerState state, bool close)
        {
            // A player's signaling socket reconnecting says nothing about its data channel,
            // which runs peer to peer; only a negotiation still waiting on signaling is lost.
            Assert.Equal(close, PeerStateWatcher.ShouldCloseOnSignalingLeave(state));
        }
    }
}

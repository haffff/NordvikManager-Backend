using NordvikManager.WebRTC;
using SIPSorcery.Net;
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
        [InlineData(RTCPeerConnectionState.failed)]
        [InlineData(RTCPeerConnectionState.closed)]
        public void FailedOrClosed_IsDeadAtOnce(RTCPeerConnectionState state)
        {
            var watcher = Create();
            watcher.OnStateChanged(state);
            Assert.Equal(1, _deadCount);
        }

        [Fact]
        public void Disconnected_IsNotDeadYet()
        {
            var watcher = Create();
            watcher.OnStateChanged(RTCPeerConnectionState.disconnected);
            Assert.Equal(0, _deadCount);
        }

        [Fact]
        public async Task Disconnected_PastTheGracePeriod_IsDead()
        {
            var watcher = Create();
            watcher.OnStateChanged(RTCPeerConnectionState.disconnected);

            _graceElapsed!.SetResult();
            await Task.Yield();

            Assert.Equal(1, _deadCount);
        }

        [Fact]
        public async Task Disconnected_ThenConnectedAgain_StaysAlive()
        {
            var watcher = Create();
            watcher.OnStateChanged(RTCPeerConnectionState.disconnected);
            watcher.OnStateChanged(RTCPeerConnectionState.connected);

            Assert.True(_graceToken.IsCancellationRequested);
            _graceElapsed!.TrySetResult();
            await Task.Yield();

            Assert.Equal(0, _deadCount);
        }

        [Fact]
        public void IsDeadOnlyOnce()
        {
            var watcher = Create();
            watcher.OnStateChanged(RTCPeerConnectionState.failed);
            watcher.OnStateChanged(RTCPeerConnectionState.closed);
            Assert.Equal(1, _deadCount);
        }

        [Theory]
        [InlineData(RTCPeerConnectionState.@new, true)]
        [InlineData(RTCPeerConnectionState.connecting, true)]
        [InlineData(RTCPeerConnectionState.connected, false)]
        [InlineData(RTCPeerConnectionState.disconnected, false)]
        public void SignalingLeave_ClosesOnlyAPeerThatIsStillNegotiating(RTCPeerConnectionState state, bool close)
        {
            // A player's signaling socket reconnecting says nothing about its data channel,
            // which runs peer to peer; only a negotiation still waiting on signaling is lost.
            Assert.Equal(close, PeerStateWatcher.ShouldCloseOnSignalingLeave(state));
        }
    }
}

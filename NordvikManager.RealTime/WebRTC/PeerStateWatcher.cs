using System;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Decides when a browser peer's connection is gone for good. 'failed' and 'closed' are;
    /// 'disconnected' is a pause ICE often recovers from (a Wi-Fi blip, a laptop lid), so it
    /// only counts once it lasted the grace period without going back to 'connected'.
    /// <c>onDead</c> runs at most once.
    /// </summary>
    public sealed class PeerStateWatcher
    {
        public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(10);

        private readonly TimeSpan _grace;
        private readonly Action _onDead;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly object _lock = new();
        private CancellationTokenSource? _pending;
        private int _dead;

        public PeerStateWatcher(TimeSpan grace, Action onDead, Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _grace = grace;
            _onDead = onDead;
            _delay = delay ?? Task.Delay;
        }

        public void OnStateChanged(PeerState state)
        {
            switch (state)
            {
                case PeerState.Failed:
                case PeerState.Closed:
                    CancelPending();
                    Die();
                    break;
                case PeerState.Disconnected:
                    StartGrace();
                    break;
                default:
                    CancelPending();
                    break;
            }
        }

        /// <summary>
        /// Whether a player's signaling socket leaving should close its peer connection. The
        /// data channel runs peer to peer, so a socket reconnecting says nothing about it; only
        /// a negotiation still waiting on signaling (offer, answer, candidates) is lost.
        /// </summary>
        public static bool ShouldCloseOnSignalingLeave(PeerState state)
            => state is PeerState.New or PeerState.Connecting;

        private void StartGrace()
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                if (_pending != null) return; // already waiting
                cts = _pending = new CancellationTokenSource();
            }
            _ = WaitThenDieAsync(cts);
        }

        private async Task WaitThenDieAsync(CancellationTokenSource cts)
        {
            try
            {
                await _delay(_grace, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (cts.IsCancellationRequested) return;
            Die();
        }

        private void CancelPending()
        {
            lock (_lock)
            {
                _pending?.Cancel();
                _pending = null;
            }
        }

        private void Die()
        {
            if (Interlocked.Exchange(ref _dead, 1) == 0)
                _onDead();
        }
    }
}

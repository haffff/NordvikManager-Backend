using System;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Brings a GM backend's signaling socket back after it dropped (network, a Central
    /// restart or eviction). SocketIOClient only retries the first connect, so without this
    /// the backend stayed off signaling (no new players, no reconnects) until the GM reopened
    /// the game.
    /// </summary>
    public static class SignalingReconnect
    {
        public enum Outcome { Connected, Refused, Stopped }

        private static readonly TimeSpan[] Delays =
        {
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30),
        };

        public static TimeSpan DelayFor(int attempt) => Delays[Math.Clamp(attempt, 0, Delays.Length - 1)];

        /// <summary>
        /// Waits, then calls <paramref name="connect"/>, until it succeeds, Central refuses
        /// (a <see cref="SignalingAuthException"/>: retrying can't help) or
        /// <paramref name="keepGoing"/> says the session no longer wants signaling.
        /// </summary>
        public static async Task<Outcome> RunAsync(
            Func<Task> connect,
            Func<bool> keepGoing,
            Func<TimeSpan, Task> delay,
            Action<int, Exception>? onAttemptFailed = null)
        {
            for (var attempt = 0; ; attempt++)
            {
                if (!keepGoing()) return Outcome.Stopped;
                await delay(DelayFor(attempt));
                if (!keepGoing()) return Outcome.Stopped;
                try
                {
                    await connect();
                    return Outcome.Connected;
                }
                catch (SignalingAuthException)
                {
                    return Outcome.Refused;
                }
                catch (Exception ex)
                {
                    onAttemptFailed?.Invoke(attempt + 1, ex);
                }
            }
        }
    }
}

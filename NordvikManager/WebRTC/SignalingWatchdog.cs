using System;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Notices a GM signaling connection that died without a disconnect. SocketIOClient
    /// doesn't time out missing server pings, so a half-open connection (e.g. after a Wi-Fi
    /// switch) looked connected forever while Central had dropped it. Every
    /// <see cref="Interval"/> Central is asked (gm-alive, acknowledged); no answer, or an answer
    /// that this socket isn't the session's GM, ends it. A Central that never answered (one
    /// from before gm-alive) is left alone.
    /// </summary>
    public static class SignalingWatchdog
    {
        public enum Answer { None, Gm, NotGm }

        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(20);

        /// <summary>Runs until <paramref name="keepGoing"/> is false or the connection is found dead.</summary>
        public static async Task RunAsync(
            Func<Task<Answer>> ask,
            Func<bool> keepGoing,
            Func<Task> onDead,
            Func<TimeSpan, Task> delay)
        {
            var answeredBefore = false;
            while (true)
            {
                // Checked after the wait: it starts on "authenticated", which can come before
                // the connection is recorded as the game's current one.
                await delay(Interval);
                if (!keepGoing()) return;

                var answer = await ask();
                if (answer == Answer.Gm)
                {
                    answeredBefore = true;
                    continue;
                }
                if (answer == Answer.NotGm || answeredBefore)
                {
                    await onDead();
                    return;
                }
            }
        }
    }
}

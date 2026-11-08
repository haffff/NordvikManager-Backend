using System.Collections.Generic;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// Puts a data channel's chunked messages back together. Ids, positions and totals come
    /// from the player's client, so everything it can make this hold is limited. One per data
    /// channel; not thread-safe (the channel delivers its messages one at a time).
    /// </summary>
    public class ChunkAssembler
    {
        public enum Outcome
        {
            /// <summary>More chunks of this message are expected.</summary>
            Waiting,
            /// <summary>That was the last chunk: the message is complete.</summary>
            Complete,
            /// <summary>Over a limit or malformed: the chunk is dropped, with what the message had so far.</summary>
            Rejected,
        }

        // The client's limit for what it receives (MAX_INCOMING_CHUNKS), at its 15,000 per chunk.
        public const int DefaultMaxChunks = 20_000;
        public const long DefaultMaxPendingChars = DefaultMaxChunks * 15_000L;

        // A client sends its large messages a few at a time.
        public const int DefaultMaxPendingMessages = 64;

        private readonly int _maxChunks;
        private readonly int _maxPendingMessages;
        private readonly long _maxPendingChars;
        private readonly Dictionary<string, PendingMessage> _pending = new();
        private long _heldChars;

        public ChunkAssembler(
            int maxChunks = DefaultMaxChunks,
            int maxPendingMessages = DefaultMaxPendingMessages,
            long maxPendingChars = DefaultMaxPendingChars)
        {
            _maxChunks = maxChunks;
            _maxPendingMessages = maxPendingMessages;
            _maxPendingChars = maxPendingChars;
        }

        /// <summary>Stores a chunk; <paramref name="message"/> is set when the outcome is Complete.</summary>
        public Outcome Add(string id, int index, int total, string data, out string? message)
        {
            message = null;

            if (total < 1 || total > _maxChunks || index < 0 || index >= total)
                return Reject(id);

            if (!_pending.TryGetValue(id, out var pending))
            {
                if (_pending.Count >= _maxPendingMessages)
                    return Outcome.Rejected;
                pending = new PendingMessage(total);
                _pending[id] = pending;
            }
            else if (pending.Parts.Length != total)
            {
                return Reject(id);
            }

            var previous = pending.Parts[index];
            var growth = data.Length - (previous?.Length ?? 0);
            if (_heldChars + growth > _maxPendingChars)
                return Reject(id);

            _heldChars += growth;
            pending.Chars += growth;
            pending.Parts[index] = data;
            if (previous == null)
                pending.Received++;

            if (pending.Received < total)
                return Outcome.Waiting;

            Forget(id);
            message = string.Concat(pending.Parts);
            return Outcome.Complete;
        }

        private Outcome Reject(string id)
        {
            Forget(id);
            return Outcome.Rejected;
        }

        private void Forget(string id)
        {
            if (_pending.Remove(id, out var pending))
                _heldChars -= pending.Chars;
        }

        private sealed class PendingMessage
        {
            public PendingMessage(int total) => Parts = new string?[total];

            public string?[] Parts { get; }
            public int Received { get; set; }
            public long Chars { get; set; }
        }
    }
}

using System;
using System.Collections.Concurrent;

namespace NordvikManager.Services
{
    /// <summary>
    /// What became of each long-running operation (addon install, folder linking...), kept
    /// for an hour. The outcome reaches the player as one push (OperationProgressPublisher);
    /// when that push is lost, the client asks for it here (GET api/operation/status), so its
    /// progress toast doesn't spin until it times out. In memory: a restart forgets them,
    /// which the client takes as the operation being lost.
    /// </summary>
    public class OperationResults
    {
        public const string Running = "running";
        public const string Complete = "complete";
        public const string Failed = "failed";

        public static readonly TimeSpan Keep = TimeSpan.FromHours(1);

        /// <summary>The one the publisher records into and the status endpoint reads.</summary>
        public static OperationResults Shared { get; } = new();

        public sealed record Entry(Guid PlayerId, string Status, string? Title, string? Description, DateTime At);

        private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
        private readonly Func<DateTime> _now;

        public OperationResults(Func<DateTime>? now = null)
        {
            _now = now ?? (() => DateTime.UtcNow);
        }

        public void Begin(Guid operationId, Guid playerId)
        {
            Prune();
            _entries[operationId] = new Entry(playerId, Running, null, null, _now());
        }

        public void Finish(Guid operationId, Guid playerId, bool failed, string? title, string? description)
        {
            _entries[operationId] = new Entry(playerId, failed ? Failed : Complete, title, description, _now());
        }

        /// <summary>The operation's state, for the player who started it only; null when unknown.</summary>
        public Entry? Get(Guid operationId, Guid playerId)
        {
            if (!_entries.TryGetValue(operationId, out var entry)) return null;
            if (entry.PlayerId != playerId || _now() - entry.At > Keep) return null;
            return entry;
        }

        private void Prune()
        {
            var now = _now();
            foreach (var pair in _entries)
                if (now - pair.Value.At > Keep)
                    _entries.TryRemove(pair.Key, out _);
        }
    }
}

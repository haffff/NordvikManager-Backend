using DNDOnePlaceManager.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Thread-safe, in-memory circular event log for a single game session.
    /// Keeps the most recent <see cref="MaxEntries"/> entries; older ones are dropped.
    /// </summary>
    public class GameEventLog
    {
        public const int MaxEntries = 500;

        private readonly List<GameEventEntry> _entries = new();
        private readonly object _lock = new();

        public void Log(string level, string category, string message, string? player = null, object? details = null)
        {
            var entry = new GameEventEntry
            {
                Level    = level,
                Category = category,
                Message  = message,
                Player   = player,
                Details  = details != null ? JsonConvert.SerializeObject(details) : null,
            };

            lock (_lock)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                    _entries.RemoveAt(0);
            }
        }

        /// <summary>
        /// Returns a snapshot of all entries, optionally filtered to those added after
        /// a given entry ID (exclusive). Use for incremental polling.
        /// </summary>
        public IReadOnlyList<GameEventEntry> GetEntries(Guid? sinceId = null)
        {
            lock (_lock)
            {
                if (sinceId == null || sinceId == Guid.Empty)
                    return _entries.ToArray();

                var idx = _entries.FindLastIndex(e => e.Id == sinceId.Value);
                if (idx < 0 || idx == _entries.Count - 1)
                    return Array.Empty<GameEventEntry>();

                return _entries.GetRange(idx + 1, _entries.Count - idx - 1);
            }
        }

        public void Clear()
        {
            lock (_lock)
                _entries.Clear();
        }
    }
}

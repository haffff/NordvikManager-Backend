using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace DndOnePlaceManager.Application.Services.Rolls
{
    /// <summary>One evaluated inline roll, keyed by the name the caller gave it.</summary>
    public class RollResultEntry
    {
        public string Key { get; set; }
        public RollDefinition Roll { get; set; }
    }

    /// <summary>
    /// Rolls evaluated server-side but not yet posted to chat (see RollsController).
    /// The results never leave the server's control: a later post references the
    /// session by id, and chat shows these numbers, not client-supplied ones.
    /// </summary>
    public class RollSession
    {
        public Guid Id { get; init; }
        public Guid GameId { get; init; }
        public Guid PlayerId { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public IReadOnlyList<RollResultEntry> Results { get; init; }
    }

    public interface IRollSessionStore
    {
        RollSession Create(Guid gameId, Guid playerId, IReadOnlyList<RollResultEntry> results);

        /// <summary>
        /// Removes and returns the session if it exists, hasn't expired, and belongs to
        /// this game and player. Someone else's session is left untouched.
        /// </summary>
        bool TryTake(Guid rollId, Guid gameId, Guid playerId, out RollSession session);
    }

    /// <summary>
    /// In-memory, bounded: a session a client starts but never finishes (a sheet
    /// script that calls startRoll and then errors, a closed tab) expires after
    /// <see cref="Ttl"/>, and a player can hold at most
    /// <see cref="MaxPendingPerPlayer"/> — the oldest are dropped first. Expired
    /// sessions are swept on each Create, so memory stays proportional to active
    /// players rather than to rolls ever made.
    /// </summary>
    public class RollSessionStore : IRollSessionStore
    {
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
        public const int MaxPendingPerPlayer = 50;

        private readonly ConcurrentDictionary<Guid, RollSession> _sessions = new();
        private readonly Func<DateTimeOffset> _now;

        public RollSessionStore() : this(() => DateTimeOffset.UtcNow) { }

        public RollSessionStore(Func<DateTimeOffset> now)
        {
            _now = now;
        }

        public int Count => _sessions.Count;

        public RollSession Create(Guid gameId, Guid playerId, IReadOnlyList<RollResultEntry> results)
        {
            var now = _now();
            SweepExpired(now);

            var pending = _sessions.Values
                .Where(s => s.GameId == gameId && s.PlayerId == playerId)
                .OrderBy(s => s.CreatedAt)
                .ToList();
            foreach (var stale in pending.Take(Math.Max(0, pending.Count - (MaxPendingPerPlayer - 1))))
                _sessions.TryRemove(stale.Id, out _);

            var session = new RollSession
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                PlayerId = playerId,
                CreatedAt = now,
                Results = results,
            };
            _sessions[session.Id] = session;
            return session;
        }

        public bool TryTake(Guid rollId, Guid gameId, Guid playerId, out RollSession session)
        {
            session = null;
            if (!_sessions.TryGetValue(rollId, out var found))
                return false;
            if (found.GameId != gameId || found.PlayerId != playerId)
                return false;
            if (!_sessions.TryRemove(rollId, out found))
                return false;
            if (IsExpired(found, _now()))
                return false;

            session = found;
            return true;
        }

        private static bool IsExpired(RollSession session, DateTimeOffset now) => now - session.CreatedAt > Ttl;

        private void SweepExpired(DateTimeOffset now)
        {
            foreach (var session in _sessions.Values)
            {
                if (IsExpired(session, now))
                    _sessions.TryRemove(session.Id, out _);
            }
        }
    }
}

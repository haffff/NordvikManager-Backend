using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.WebRTC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NordvikManager.Services
{
    /// <summary>
    /// The players in a lobby and their connections. Joins, leaves and kicks happen on their
    /// own threads while others are broadcasting, so everything read here is a snapshot and
    /// a player's connection list is replaced, never changed: a list already handed out stays
    /// as it was. Adding or removing a connection goes through <see cref="Attach"/> and
    /// <see cref="Detach"/>, so only one caller ever sees a player arrive or go.
    /// </summary>
    public sealed class ConnectedPlayerMap : IEnumerable<KeyValuePair<PlayerDTO, IReadOnlyList<IPlayerConnection>>>
    {
        private static readonly IReadOnlyList<IPlayerConnection> NoConnections = Array.Empty<IPlayerConnection>();

        private readonly object _lock = new();
        private readonly Dictionary<PlayerDTO, IReadOnlyList<IPlayerConnection>> _players = new();

        public IReadOnlyList<IPlayerConnection> this[PlayerDTO player]
        {
            get { lock (_lock) return _players[player]; }
            set { lock (_lock) _players[player] = value.ToList(); }
        }

        public List<PlayerDTO> Keys { get { lock (_lock) return _players.Keys.ToList(); } }

        public int Count { get { lock (_lock) return _players.Count; } }

        public bool ContainsKey(PlayerDTO player) { lock (_lock) return _players.ContainsKey(player); }

        public bool TryGetValue(PlayerDTO player, out IReadOnlyList<IPlayerConnection> connections)
        {
            lock (_lock) return _players.TryGetValue(player, out connections);
        }

        /// <summary>The player's connections; none if they aren't (or are no longer) here.</summary>
        public IReadOnlyList<IPlayerConnection> ConnectionsOf(PlayerDTO player)
        {
            lock (_lock) return _players.TryGetValue(player, out var connections) ? connections : NoConnections;
        }

        public bool Remove(PlayerDTO player) { lock (_lock) return _players.Remove(player); }

        public void Clear() { lock (_lock) _players.Clear(); }

        /// <summary>
        /// Adds the connection to the player with this id, creating the entry if there is none.
        /// Returns the entry's key, which is what the rest of the lobby knows the player by.
        /// </summary>
        public PlayerDTO Attach(PlayerDTO player, IPlayerConnection connection, out bool isNewPlayer)
        {
            lock (_lock)
            {
                foreach (var (key, connections) in _players)
                {
                    if (key.Id != player.Id)
                        continue;
                    _players[key] = new List<IPlayerConnection>(connections) { connection };
                    isNewPlayer = false;
                    return key;
                }

                _players[player] = new List<IPlayerConnection> { connection };
                isNewPlayer = true;
                return player;
            }
        }

        /// <summary>Removes the connection. True only for the call that took the player's last one.</summary>
        public bool Detach(PlayerDTO player, IPlayerConnection connection)
        {
            lock (_lock)
            {
                if (!_players.TryGetValue(player, out var connections))
                    return false;

                var left = connections.Where(c => !ReferenceEquals(c, connection)).ToList();
                if (left.Count > 0)
                {
                    _players[player] = left;
                    return false;
                }

                _players.Remove(player);
                return true;
            }
        }

        public IEnumerator<KeyValuePair<PlayerDTO, IReadOnlyList<IPlayerConnection>>> GetEnumerator()
        {
            lock (_lock) return _players.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

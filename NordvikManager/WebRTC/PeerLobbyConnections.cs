using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace NordvikManager.WebRTC
{
    /// <summary>
    /// The lobby connection each browser peer (signaling socket id) opened, so the peer going
    /// away removes exactly that connection, once. Several things report a peer going away
    /// (its state change, its data channel closing, the cleanup closing it); looking the
    /// connection up by user instead would remove the same user's other connection, e.g.
    /// another tab or the one a reconnect just opened, leaving that channel open but out of
    /// the lobby, so it no longer got any pushes.
    /// </summary>
    public sealed class PeerLobbyConnections
    {
        private readonly ConcurrentDictionary<string, (string GameId, string UserId, IPlayerConnection Connection)> _bySocket = new();
        private readonly Func<string, string, IPlayerConnection, Task> _remove;

        /// <param name="remove">Removes (gameId, userId, connection) from its lobby.</param>
        public PeerLobbyConnections(Func<string, string, IPlayerConnection, Task> remove)
        {
            _remove = remove;
        }

        public void Track(string socketId, string gameId, string userId, IPlayerConnection connection)
            => _bySocket[socketId] = (gameId, userId, connection);

        /// <summary>The socket is gone: release whatever connection it has.</summary>
        public Task ReleaseAsync(string socketId)
            => _bySocket.TryRemove(socketId, out var entry)
                ? _remove(entry.GameId, entry.UserId, entry.Connection)
                : Task.CompletedTask;

        /// <summary>
        /// One connection is gone: release it only if it is still the socket's connection, not
        /// one that replaced it after the player re-offered on the same socket.
        /// </summary>
        public Task ReleaseAsync(string socketId, IPlayerConnection connection)
        {
            if (!_bySocket.TryGetValue(socketId, out var entry) || !ReferenceEquals(entry.Connection, connection))
                return Task.CompletedTask;
            return _bySocket.TryRemove(new(socketId, entry))
                ? _remove(entry.GameId, entry.UserId, entry.Connection)
                : Task.CompletedTask;
        }
    }
}

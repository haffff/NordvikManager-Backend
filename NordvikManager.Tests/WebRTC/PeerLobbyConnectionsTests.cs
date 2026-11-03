using NordvikManager.WebRTC;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // A peer's lobby connection is let go of once, and only that connection: the same user's
    // other connections (another tab, or the connection a reconnect just made) stay in the lobby.
    public class PeerLobbyConnectionsTests
    {
        private sealed class FakeConnection : IPlayerConnection
        {
            public Task<bool> SendMessageToPlayer(object message) => Task.FromResult(true);
        }

        private readonly List<(string GameId, string UserId, IPlayerConnection Connection)> _removed = new();

        private PeerLobbyConnections Create() => new((gameId, userId, connection) =>
        {
            _removed.Add((gameId, userId, connection));
            return Task.CompletedTask;
        });

        [Fact]
        public async Task Release_RemovesThatPeersConnection_NotTheUsersFirstOne()
        {
            var connections = Create();
            var oldTab = new FakeConnection();
            var newTab = new FakeConnection();
            connections.Track("socket-old", "game", "user", oldTab);
            connections.Track("socket-new", "game", "user", newTab);

            await connections.ReleaseAsync("socket-new");

            Assert.Single(_removed);
            Assert.Same(newTab, _removed[0].Connection);
            Assert.Equal(("game", "user"), (_removed[0].GameId, _removed[0].UserId));
        }

        [Fact]
        public async Task Release_Twice_RemovesOnce()
        {
            // The peer's state change, its data channel's close and the cleanup's own close
            // all report the same peer going away.
            var connections = Create();
            var oldTab = new FakeConnection();
            var newTab = new FakeConnection();
            connections.Track("socket-old", "game", "user", oldTab);
            connections.Track("socket-new", "game", "user", newTab);

            await connections.ReleaseAsync("socket-old");
            await connections.ReleaseAsync("socket-old");
            await connections.ReleaseAsync("socket-old");

            Assert.Single(_removed);
            Assert.Same(oldTab, _removed[0].Connection);
        }

        [Fact]
        public async Task ReleasingAnOldConnection_LeavesTheOneThatReplacedItOnTheSameSocket()
        {
            // The player re-offered on the same signaling socket: the old connection's late
            // close must not take the new one out of the lobby.
            var connections = Create();
            var oldConnection = new FakeConnection();
            var newConnection = new FakeConnection();
            connections.Track("socket", "game", "user", oldConnection);
            connections.Track("socket", "game", "user", newConnection);

            await connections.ReleaseAsync("socket", oldConnection);
            Assert.Empty(_removed);

            await connections.ReleaseAsync("socket", newConnection);
            Assert.Single(_removed);
            Assert.Same(newConnection, _removed[0].Connection);
        }

        [Fact]
        public async Task Release_OfAnUntrackedPeer_DoesNothing()
        {
            var connections = Create();
            await connections.ReleaseAsync("never-opened");
            Assert.Empty(_removed);
        }
    }
}

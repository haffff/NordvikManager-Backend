using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // Joins, leaves and kicks change the lobby's player list while other connections are
    // broadcasting to it, each on its own thread.
    public class ConnectedPlayerMapTests
    {
        private sealed class Connection : IPlayerConnection
        {
            public Task<bool> SendMessageToPlayer(object message) => Task.FromResult(true);
        }

        private static PlayerDTO Player(Guid? id = null) => new() { Id = id ?? Guid.NewGuid(), Name = "P" };

        [Fact]
        public void Enumerating_IsNotDisturbed_ByAJoinOrLeaveHalfWay()
        {
            var map = new ConnectedPlayerMap();
            var (a, b) = (Player(), Player());
            map[a] = new List<IPlayerConnection> { new Connection() };
            map[b] = new List<IPlayerConnection> { new Connection() };

            var seen = 0;
            foreach (var _ in map)
            {
                seen++;
                map.Attach(Player(), new Connection(), out var _);
                map.Remove(b);
            }

            Assert.Equal(2, seen);
        }

        [Fact]
        public void AListAlreadyHandedOut_DoesNotChange_WhenAConnectionIsAdded()
        {
            var map = new ConnectedPlayerMap();
            var player = Player();
            map.Attach(player, new Connection(), out _);
            var before = map[player];

            map.Attach(player, new Connection(), out _);

            Assert.Single(before);
            Assert.Equal(2, map[player].Count);
        }

        [Fact]
        public void Attach_ReturnsTheKeyAlreadyThere_ForTheSamePlayerId()
        {
            var map = new ConnectedPlayerMap();
            var id = Guid.NewGuid();
            var first = Player(id);
            map.Attach(first, new Connection(), out var firstIsNew);

            var key = map.Attach(Player(id), new Connection(), out var secondIsNew);

            Assert.True(firstIsNew);
            Assert.False(secondIsNew);
            Assert.Same(first, key);
            Assert.Single(map.Keys);
        }

        [Fact]
        public async Task Attach_FromManyThreads_MakesOnePlayer_AndKeepsEveryConnection()
        {
            var map = new ConnectedPlayerMap();
            var id = Guid.NewGuid();
            var news = 0;

            await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => Task.Run(() =>
            {
                map.Attach(Player(id), new Connection(), out var isNew);
                if (isNew)
                    Interlocked.Increment(ref news);
            })));

            Assert.Equal(1, news);
            Assert.Equal(200, map[map.Keys.Single()].Count);
        }

        [Fact]
        public void Detach_SaysWhenTheLastConnectionWent_AndDropsThePlayer()
        {
            var map = new ConnectedPlayerMap();
            var player = Player();
            var (one, two) = (new Connection(), new Connection());
            map.Attach(player, one, out _);
            map.Attach(player, two, out _);

            Assert.False(map.Detach(player, one));
            Assert.True(map.ContainsKey(player));
            Assert.True(map.Detach(player, two));
            Assert.False(map.ContainsKey(player));
        }

        [Fact]
        public void Detach_TellsOnlyOneCaller_ThatThePlayerLeft()
        {
            var map = new ConnectedPlayerMap();
            var player = Player();
            var connection = new Connection();
            map.Attach(player, connection, out _);

            Assert.True(map.Detach(player, connection));
            Assert.False(map.Detach(player, connection));
        }

        [Fact]
        public void ConnectionsOf_IsEmpty_ForAPlayerWhoHasLeft()
        {
            Assert.Empty(new ConnectedPlayerMap().ConnectionsOf(Player()));
        }
    }
}

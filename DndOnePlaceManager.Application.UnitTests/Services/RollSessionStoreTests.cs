using DndOnePlaceManager.Application.Services.Implementations.ChatTemplates;
using DndOnePlaceManager.Application.Services.Rolls;
using System;
using System.Collections.Generic;
using Xunit;

namespace DndOnePlaceManager.Application.UnitTests.Services
{
    public class RollSessionStoreTests
    {
        private static readonly Guid Game = Guid.NewGuid();
        private static readonly Guid Player = Guid.NewGuid();

        private static IReadOnlyList<RollResultEntry> OneRoll(int result = 7) => new[]
        {
            new RollResultEntry { Key = "result", Roll = new RollDefinition { Result = result } },
        };

        private sealed class Clock
        {
            public DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        }

        [Fact]
        public void TryTake_ReturnsTheStoredResults_Once()
        {
            var store = new RollSessionStore();
            var session = store.Create(Game, Player, OneRoll(42));

            Assert.True(store.TryTake(session.Id, Game, Player, out var taken));
            Assert.Equal(42, taken.Results[0].Roll.Result);
            Assert.False(store.TryTake(session.Id, Game, Player, out _));
        }

        [Fact]
        public void TryTake_RefusesAnotherPlayersOrGamesSession_AndLeavesItForTheOwner()
        {
            var store = new RollSessionStore();
            var session = store.Create(Game, Player, OneRoll());

            Assert.False(store.TryTake(session.Id, Game, Guid.NewGuid(), out _));
            Assert.False(store.TryTake(session.Id, Guid.NewGuid(), Player, out _));
            Assert.True(store.TryTake(session.Id, Game, Player, out _));
        }

        [Fact]
        public void TryTake_RefusesAnExpiredSession()
        {
            var clock = new Clock();
            var store = new RollSessionStore(() => clock.Now);
            var session = store.Create(Game, Player, OneRoll());

            clock.Now += RollSessionStore.Ttl + TimeSpan.FromSeconds(1);

            Assert.False(store.TryTake(session.Id, Game, Player, out _));
        }

        [Fact]
        public void Create_SweepsExpiredSessions_SoAbandonedRollsDontAccumulate()
        {
            var clock = new Clock();
            var store = new RollSessionStore(() => clock.Now);
            for (var i = 0; i < 10; i++)
                store.Create(Game, Guid.NewGuid(), OneRoll());

            clock.Now += RollSessionStore.Ttl + TimeSpan.FromSeconds(1);
            store.Create(Game, Player, OneRoll());

            Assert.Equal(1, store.Count);
        }

        [Fact]
        public void Create_CapsPendingSessionsPerPlayer_DroppingTheOldest()
        {
            var clock = new Clock();
            var store = new RollSessionStore(() => clock.Now);
            var first = store.Create(Game, Player, OneRoll());
            for (var i = 0; i < RollSessionStore.MaxPendingPerPlayer; i++)
            {
                clock.Now += TimeSpan.FromMilliseconds(1);
                store.Create(Game, Player, OneRoll());
            }
            var otherPlayers = store.Create(Game, Guid.NewGuid(), OneRoll());

            Assert.Equal(RollSessionStore.MaxPendingPerPlayer + 1, store.Count);
            Assert.False(store.TryTake(first.Id, Game, Player, out _));
            Assert.True(store.TryTake(otherPlayers.Id, Game, otherPlayers.PlayerId, out _));
        }
    }
}

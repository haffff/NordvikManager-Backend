using DndOnePlaceManager.Domain.Entities.Resources;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DNDOnePlaceManager.Services.Implementations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Services.Implementations
{
    // %qn:resource-"<key or name>".id% lets an addon's action find one of its own
    // resources, e.g. a theme's install action adding its stylesheet to the game.
    public class ActionPropertyQueryResolverTests : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DndOneContext _db;
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly Guid _otherGameId = Guid.NewGuid();

        public ActionPropertyQueryResolverTests()
        {
            _connection.Open();
            _db = new DndOneContext(new DbContextOptionsBuilder<DndOneContext>().UseSqlite(_connection).Options);
            _db.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }

        private Guid SeedResource(Guid gameId, string name, string key)
        {
            if (_db.Games.Find(gameId) == null)
            {
                var playerId = Guid.NewGuid();
                _db.Games.Add(new GameModel { Id = gameId, Name = "G", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel> { new PlayerModel { Id = playerId, Name = "P" } } });
                _db.SaveChanges();
            }
            var resource = new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = _db.Players.First(p => p.Game.Id == gameId).Id, Name = name, Key = key, Data = new byte[] { 1 } };
            _db.Resources.Add(resource);
            _db.SaveChanges();
            return resource.Id;
        }

        private Task<string> Resolve(string raw) =>
            new ActionPropertyQueryResolver(_db, _gameId).PreResolveQueriesAsync(raw, new Dictionary<string, object>());

        [Fact]
        public async Task Resource_ByKey_ResolvesItsId()
        {
            var id = SeedResource(_gameId, "theme.css", "theme_im_theme.css");

            Assert.Equal(id.ToString(), await Resolve("%qn:resource-\"theme_im_theme.css\".id%"));
        }

        [Fact]
        public async Task Resource_ByName_ResolvesItsId()
        {
            var id = SeedResource(_gameId, "parchment.svg", "some_key");

            Assert.Equal(id.ToString(), await Resolve("%qn:resource-\"parchment.svg\".id%"));
        }

        [Fact]
        public async Task Resource_OfAnotherGame_IsNotFound()
        {
            SeedResource(_otherGameId, "theme.css", "theme_im_theme.css");

            Assert.Equal("", await Resolve("%qn:resource-\"theme_im_theme.css\".id%"));
        }
    }
}

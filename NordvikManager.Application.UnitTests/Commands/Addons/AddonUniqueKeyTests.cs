using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // The install checks for an addon before adding it, but two installs at once (two GMs,
    // or the background install at game creation) could both pass that check and add the
    // addon twice, each copy then running its hooks. The database refuses the second now.
    public class AddonUniqueKeyTests : SqliteHandlerTestBase
    {
        private GameModel Game()
        {
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid(), Maps = new(), Players = new() };
            using var seed = SeedContext();
            seed.Games.Add(game);
            seed.SaveChanges();
            return game;
        }

        private static void AddAddon(DbContext db, Guid gameId, string key)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key };
            db.Add(addon);
            db.Entry(addon).Property("GameModelId").CurrentValue = gameId;
        }

        [Fact]
        public void SameKeyTwiceInOneGame_IsRefused()
        {
            var game = Game();
            AddAddon(Db, game.Id, "dnd5e");
            Db.SaveChanges();

            using var second = SeedContext();
            AddAddon(second, game.Id, "dnd5e");

            Assert.Throws<DbUpdateException>(() => second.SaveChanges());
        }

        [Fact]
        public void SameKeyInTwoGames_IsAllowed()
        {
            var one = Game();
            var two = Game();
            AddAddon(Db, one.Id, "dnd5e");
            AddAddon(Db, two.Id, "dnd5e");

            Db.SaveChanges();

            Assert.Equal(2, SeedContext().Addons.Count(a => a.Key == "dnd5e"));
        }
    }
}

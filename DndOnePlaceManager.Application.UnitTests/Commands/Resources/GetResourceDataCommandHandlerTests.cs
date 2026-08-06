using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Domain.Entities.Resources;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using System.Text;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // Regression test for a real bug: the handler used to resolve the resource row via
    // FirstOrDefaultAsync(x => x.Id == id || x.Key == key) with NO GameId in the
    // predicate, checking GameId only *after* picking a row. A resource Key is only
    // unique per-game (SetResourceCommandHandler upserts by (GameId, Key)), so the same
    // key can legitimately exist under multiple games — and whichever row EF happened to
    // return first would silently shadow the correct game's own resource, producing a
    // false "not found" even though the right row existed.
    public class GetResourceDataCommandHandlerTests : HandlerTestBase
    {
        private GetResourceDataCommandHandler Handler() => new(Mapper, Db, Storage);

        // BuildGame() always seeds a PlayerModel with the same shared PlayerId, so calling
        // it twice in one test throws an EF identity conflict. The handler under test never
        // reads Game.Players (its "player" lookup is dead/commented-out code — see the
        // Handle method), so a second bare game with no player is enough here.
        private GameModel BuildBareGame()
        {
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Other Game", SystemPlayerId = Guid.NewGuid() };
            Db.Games.Add(game);
            Db.SaveChanges();
            return game;
        }

        private Guid SeedResource(Guid gameId, string key, byte[] data)
        {
            var id = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel
            {
                Id = id,
                GameId = gameId,
                Key = key,
                Name = key,
                Data = data,
                MimeType = MimeType.JSON,
                PlayerId = PlayerId,
            });
            Db.SaveChanges();
            return id;
        }

        [Fact]
        public async Task Handle_SameKeyAcrossTwoGames_EachGameReadsItsOwnResource()
        {
            var gameA = BuildGame();
            var gameB = BuildBareGame();

            SeedResource(gameA.Id, "dnd5e_spells_srd", Encoding.UTF8.GetBytes("{\"game\":\"A\"}"));
            SeedResource(gameB.Id, "dnd5e_spells_srd", Encoding.UTF8.GetBytes("{\"game\":\"B\"}"));

            var (dataA, _) = await Handler().Handle(new GetResourceDataCommand
            {
                GameID = gameA.Id,
                Key = "dnd5e_spells_srd",
                Player = Player(),
            }, CancellationToken.None);

            var (dataB, _) = await Handler().Handle(new GetResourceDataCommand
            {
                GameID = gameB.Id,
                Key = "dnd5e_spells_srd",
                Player = Player(),
            }, CancellationToken.None);

            Assert.Equal("{\"game\":\"A\"}", Encoding.UTF8.GetString(dataA!));
            Assert.Equal("{\"game\":\"B\"}", Encoding.UTF8.GetString(dataB!));
        }

        [Fact]
        public async Task Handle_KeyExistsOnlyUnderAnotherGame_ReturnsNullNotWrongGamesData()
        {
            var gameA = BuildGame();
            var gameB = BuildBareGame();

            SeedResource(gameB.Id, "dnd5e_spells_srd", Encoding.UTF8.GetBytes("{\"game\":\"B\"}"));

            var (data, mimeType) = await Handler().Handle(new GetResourceDataCommand
            {
                GameID = gameA.Id,
                Key = "dnd5e_spells_srd",
                Player = Player(),
            }, CancellationToken.None);

            Assert.Null(data);
            Assert.Equal(MimeType.None, mimeType);
        }
    }
}

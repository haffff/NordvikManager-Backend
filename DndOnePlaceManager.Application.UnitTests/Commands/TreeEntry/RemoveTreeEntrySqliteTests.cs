using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    // Removing an entry by its id (the tree's delete) on real SQLite, foreign keys enforced.
    // Regression: with no TargetId in the request, "x.TargetId == request.TargetId" became
    // "TargetId IS NULL" and matched every folder, so another entry (even of another tree)
    // was picked and the delete failed with "FOREIGN KEY constraint failed".
    public class RemoveTreeEntrySqliteTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid mapsFolderId = Guid.NewGuid();
        private readonly Guid mapEntryId = Guid.NewGuid();
        private readonly Guid templateEntryId = Guid.NewGuid();
        private readonly Guid templateId = Guid.NewGuid();

        public RemoveTreeEntrySqliteTests()
        {
            using var seed = SeedContext();
            var game = new GameModel
            {
                Id = gameId, Name = "g", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "p" } },
                Cards = new List<CardModel> { new CardModel { Id = templateId, Name = "Note", IsTemplate = true } },
            };
            seed.Games.Add(game);
            // A map folder (no target) holding a map: the faulty lookup picks it.
            var folder = new TreeEntryModel { Id = mapsFolderId, Name = "Maps", IsFolder = true, Head = true, EntryType = "MapModel", Game = game };
            seed.TreeEntries.Add(folder);
            seed.TreeEntries.Add(new TreeEntryModel { Id = mapEntryId, Name = "Dungeon", Parent = folder, Head = true, TargetId = Guid.NewGuid(), EntryType = "MapModel", Game = game });
            // The template left behind by an uninstalled addon, never connected to its tree.
            seed.TreeEntries.Add(new TreeEntryModel { Id = templateEntryId, Name = "Note", TargetId = templateId, NewItem = true, EntryType = "CardTemplate", Game = game });
            seed.SaveChanges();
        }

        private Task Remove(Guid? treeEntryId = null, Guid? targetId = null) =>
            new RemoveTreeEntryCommandHandler(Db, Mapper).Handle(
                new RemoveTreeEntryCommand { GameId = gameId, PlayerId = PlayerId, TreeEntryId = treeEntryId, TargetId = targetId },
                CancellationToken.None);

        private Task<List<Guid>> RemainingIds() => SeedContext().TreeEntries.Select(x => x.Id).ToListAsync();

        [Fact]
        public async Task Handle_RemovesOnlyThatEntry_WhenRemovedById()
        {
            await Remove(treeEntryId: templateEntryId);

            var remaining = await RemainingIds();
            Assert.DoesNotContain(templateEntryId, remaining);
            Assert.Contains(mapsFolderId, remaining);
            Assert.Contains(mapEntryId, remaining);
        }

        [Fact]
        public async Task Handle_RemovesTheItemsEntry_WhenRemovedByTarget()
        {
            await Remove(targetId: templateId);

            var remaining = await RemainingIds();
            Assert.DoesNotContain(templateEntryId, remaining);
            Assert.Equal(2, remaining.Count);
        }

        [Fact]
        public async Task Handle_RemovesNothing_WhenIdIsUnknown()
        {
            await Remove(treeEntryId: Guid.NewGuid());

            Assert.Equal(3, (await RemainingIds()).Count);
        }
    }
}

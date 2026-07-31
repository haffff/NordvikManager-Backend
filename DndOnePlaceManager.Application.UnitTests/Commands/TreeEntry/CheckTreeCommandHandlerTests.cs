using DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    public class CheckTreeCommandHandlerTests : HandlerTestBase
    {
        private CheckTreeCommandHandler Handler() =>
            new(Db, Mapper, NullLogger<CheckTreeCommandHandler>.Instance);

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new CheckTreeCommand { GameID = Guid.NewGuid(), EntityType = "CardModel" };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidChain_ReturnsOkWithoutThrowing()
        {
            var game = BuildGame();
            var second = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "B", EntryType = "CardModel" };
            var first = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "A", EntryType = "CardModel", Head = true, Next = second };
            Db.TreeEntries.AddRange(first, second);
            Db.SaveChanges();

            var cmd = new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel", Fix = false };
            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_SelfReferencingNext_ThrowsTreeException()
        {
            var game = BuildGame();
            var entry = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "Loop", EntryType = "CardModel", Head = true };
            entry.Next = entry;
            Db.TreeEntries.Add(entry);
            Db.SaveChanges();

            var cmd = new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel" };

            await Assert.ThrowsAsync<TreeException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoHeadInGroup_DoesNotThrow_ReturnsOk()
        {
            var game = BuildGame();
            // No entry has Head == true for this EntryType/parent group.
            var entry = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "Orphan", EntryType = "CardModel", Head = false };
            Db.TreeEntries.Add(entry);
            Db.SaveChanges();

            var cmd = new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel" };
            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_FixEnabled_RebuildsChainInNameOrder()
        {
            var game = BuildGame();
            // Seeded out of order and disconnected — Fix should rebuild the chain sorted by Name.
            var c = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "C", EntryType = "CardModel", Head = true };
            var a = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "A", EntryType = "CardModel", Head = false };
            var b = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "B", EntryType = "CardModel", Head = false };
            Db.TreeEntries.AddRange(c, a, b);
            Db.SaveChanges();

            var cmd = new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel", Fix = true };
            await Handler().Handle(cmd, CancellationToken.None);

            var reread = Db.TreeEntries.Include(x => x.Next).ToDictionary(x => x.Name!);
            Assert.True(reread["A"].Head);
            Assert.Equal("B", reread["A"].Next!.Name);
            Assert.Equal("C", reread["B"].Next!.Name);
            Assert.Null(reread["C"].Next);
        }

        [Fact]
        public async Task Handle_FixDisabled_DoesNotModifyChain()
        {
            var game = BuildGame();
            var c = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "C", EntryType = "CardModel", Head = true };
            var a = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "A", EntryType = "CardModel", Head = false };
            Db.TreeEntries.AddRange(c, a);
            Db.SaveChanges();

            var cmd = new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel", Fix = false };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(Db.TreeEntries.Find(c.Id)!.Head);
            Assert.False(Db.TreeEntries.Find(a.Id)!.Head);
        }
    }
}

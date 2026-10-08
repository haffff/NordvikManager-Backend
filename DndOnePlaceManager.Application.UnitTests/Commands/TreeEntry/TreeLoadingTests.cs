using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree;
using DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry;
using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Commands.TreeEntry.UpdateEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    // Each tree handler used to load every tree entry of the game, of every type. One tree
    // operation must only load that tree (an addon install adds thousands of resource entries,
    // which the card tree has no business reading).
    public class TreeLoadingTests : TreeHandlerTestBase
    {
        private readonly GameModel game;
        private readonly TreeEntryModel card;

        public TreeLoadingTests()
        {
            game = BuildGame();
            card = BuildTreeEntry(game, "Goblin").GetAwaiter().GetResult();
            BuildTreeEntry(game, "Orc").GetAwaiter().GetResult();
            for (int i = 0; i < 3; i++)
                BuildTreeEntry(game, $"file{i}.png", type: "ResourceModel").GetAwaiter().GetResult();
            Db.ChangeTracker.Clear();
        }

        private void AssertOnlyCardTreeLoaded()
        {
            Assert.NotEmpty(Db.ChangeTracker.Entries<TreeEntryModel>());
            Assert.All(Db.ChangeTracker.Entries<TreeEntryModel>(), e => Assert.Equal("CardModel", e.Entity.EntryType));
        }

        [Fact]
        public async Task Add_LoadsOnlyItsTree()
        {
            await new AddTreeEntryCommandHandler(Db, Mapper).Handle(new AddTreeEntryCommand
            {
                GameId = game.Id, Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Troll", EntryType = "CardModel", TargetId = Guid.NewGuid(), AutoConnect = true },
            }, CancellationToken.None);

            AssertOnlyCardTreeLoaded();
        }

        [Fact]
        public async Task Connect_LoadsOnlyItsTree()
        {
            await new ConnectTreeEntriesCommandHandler(Db, Mapper, NullLogger<ConnectTreeEntriesCommandHandler>.Instance, new Mock<IMediator>().Object)
                .Handle(new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = "CardModel" }, CancellationToken.None);

            AssertOnlyCardTreeLoaded();
        }

        [Fact]
        public async Task Get_LoadsOnlyItsTree()
        {
            var result = await new GetTreeEntriesCommandHandler(Db, Mapper, new Mock<IMediator>().Object)
                .Handle(new GetTreeEntriesCommand { GameId = game.Id, PlayerId = PlayerId, EntityType = "CardModel" }, CancellationToken.None);

            Assert.Equal(2, result.Count);
            AssertOnlyCardTreeLoaded();
        }

        [Fact]
        public async Task Check_LoadsOnlyItsTree()
        {
            await new CheckTreeCommandHandler(Db, Mapper, NullLogger<CheckTreeCommandHandler>.Instance)
                .Handle(new CheckTreeCommand { GameID = game.Id, EntityType = "CardModel" }, CancellationToken.None);

            AssertOnlyCardTreeLoaded();
        }

        [Fact]
        public async Task Update_LoadsOnlyTheEntrysTree()
        {
            await new UpdateTreeEntryCommandHandler(Db, Mapper).Handle(new UpdateTreeEntryCommand
            {
                GameId = game.Id, PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = card.Id, Name = "Goblin chief" },
            }, CancellationToken.None);

            Assert.Equal("Goblin chief", Db.TreeEntries.Find(card.Id)!.Name);
            AssertOnlyCardTreeLoaded();
        }

        [Fact]
        public async Task Remove_LoadsOnlyTheEntrysTree()
        {
            await new RemoveTreeEntryCommandHandler(Db, Mapper)
                .Handle(new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TargetId = card.TargetId }, CancellationToken.None);

            Assert.All(Db.ChangeTracker.Entries<TreeEntryModel>(), e => Assert.Equal("CardModel", e.Entity.EntryType));
            using var check = SeedContext();
            Assert.Null(check.TreeEntries.Find(card.Id));
        }
    }
}

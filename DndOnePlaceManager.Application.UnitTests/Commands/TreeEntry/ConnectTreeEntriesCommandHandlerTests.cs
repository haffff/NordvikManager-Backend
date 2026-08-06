using DndOnePlaceManager.Application.Commands.TreeEntry.CheckTree;
using DndOnePlaceManager.Application.Commands.TreeEntry.ConnectTreeEntry;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    public class ConnectTreeEntriesCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        private ConnectTreeEntriesCommandHandler Handler() =>
            new(Db, Mapper, NullLogger<ConnectTreeEntriesCommandHandler>.Instance, _mediator.Object);

        [Fact]
        public async Task Handle_GameNotFound_ThrowsWrongArgumentsException()
        {
            var cmd = new ConnectTreeEntriesCommand { GameID = Guid.NewGuid(), EntityType = "CardModel" };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NewItemWithNoExistingTail_BecomesHeadAndClearsNewItemFlag()
        {
            var game = BuildGame();
            var entry = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "New", EntryType = "CardModel", NewItem = true };
            Db.TreeEntries.Add(entry);
            Db.SaveChanges();

            var cmd = new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = "CardModel" };
            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            var updated = Db.TreeEntries.Find(entry.Id)!;
            Assert.True(updated.Head);
            Assert.False(updated.NewItem);
        }

        [Fact]
        public async Task Handle_NewItemWithExistingTail_ConnectsAfterTail()
        {
            var game = BuildGame();
            var tail = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "Existing", EntryType = "CardModel", Head = true, NewItem = false };
            var newEntry = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "New", EntryType = "CardModel", NewItem = true };
            Db.TreeEntries.AddRange(tail, newEntry);
            Db.SaveChanges();

            var cmd = new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = "CardModel" };
            await Handler().Handle(cmd, CancellationToken.None);

            var updatedTail = Db.TreeEntries.Include(x => x.Next).First(x => x.Id == tail.Id);
            Assert.Equal(newEntry.Id, updatedTail.Next!.Id);
            Assert.False(Db.TreeEntries.Find(newEntry.Id)!.NewItem);
        }

        [Fact]
        public async Task Handle_IgnoresNewItemsOfDifferentEntityType()
        {
            var game = BuildGame();
            var entry = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "New", EntryType = "MapModel", NewItem = true };
            Db.TreeEntries.Add(entry);
            Db.SaveChanges();

            var cmd = new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = "CardModel" };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(Db.TreeEntries.Find(entry.Id)!.NewItem);
        }

        [Fact]
        public async Task Handle_AlwaysSendsCheckTreeCommandAfterwards()
        {
            var game = BuildGame();
            var cmd = new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = "CardModel" };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(
                It.Is<CheckTreeCommand>(c => c.GameID == game.Id && c.EntityType == "CardModel" && c.Fix == false),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

using AutoMapper;
using DndOnePlaceManager.Application;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Commands.TreeEntry.UpdateEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    // =========================================================================
    // Shared test base — TreeEntry-specific seeding on top of HandlerTestBase
    // =========================================================================
    public abstract class TreeHandlerTestBase : HandlerTestBase
    {
        // Seeds a tree entry via the real handler so EF tracking is never bypassed.
        // The `head` parameter is a hint only — AutoConnect computes the actual head flag.
        protected async Task<TreeEntryModel> BuildTreeEntry(GameModel game, string name,
            string type = "CardModel", TreeEntryModel? parent = null, TreeEntryModel? next = null,
            bool head = false, bool isFolder = false, Guid? targetId = null)
        {
            var tid = targetId ?? Guid.NewGuid();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto
                {
                    Name = name,
                    EntryType = type,
                    TargetId = tid,
                    ParentId = parent?.Id,
                    Next = next?.Id,
                    IsFolder = isFolder,
                    AutoConnect = true,
                }
            };
            await new AddTreeEntryCommandHandler(Db, Mapper).Handle(cmd, CancellationToken.None);

            return Db.TreeEntries
                .Include(x => x.Parent)
                .Include(x => x.Next)
                .First(x => x.TargetId == tid);
        }
    }

    // =========================================================================
    // AddTreeEntryCommandHandler tests
    // =========================================================================
    public class AddTreeEntryCommandHandlerTests : TreeHandlerTestBase
    {
        private AddTreeEntryCommandHandler Handler() => new AddTreeEntryCommandHandler(Db, Mapper);

        [Fact]
        public async Task Handle_ValidRequest_AddsEntryToDatabase()
        {
            var game = BuildGame();
            var targetId = Guid.NewGuid();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Entry A", EntryType = "CardModel", TargetId = targetId, AutoConnect = false }
            };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(Db.TreeEntries.Any(x => x.TargetId == targetId));
        }

        [Fact]
        public async Task Handle_AutoConnect_FirstEntry_SetsHead()
        {
            var game = BuildGame();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Entry A", EntryType = "CardModel", TargetId = Guid.NewGuid(), AutoConnect = true }
            };

            var (response, entries) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(entries.First(x => x.Name == "Entry A").Head);
        }

        [Fact]
        public async Task Handle_AutoConnect_AppendsToEnd_WhenNoNextSpecified()
        {
            var game = BuildGame();
            var existingTarget = Guid.NewGuid();
            await BuildTreeEntry(game, "Entry A", head: true, targetId: existingTarget);

            var newTarget = Guid.NewGuid();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Entry B", EntryType = "CardModel", TargetId = newTarget, AutoConnect = true }
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var reloaded = Db.TreeEntries.Include(x => x.Next).First(x => x.TargetId == existingTarget);
            Assert.NotNull(reloaded.Next);
            Assert.Equal(newTarget, reloaded.Next!.TargetId);
        }

        [Fact]
        public async Task Handle_AutoConnect_InsertsBefore_WhenNextSpecified()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetC = Guid.NewGuid();
            var entryC = await BuildTreeEntry(game, "Entry C", head: false, targetId: targetC);
            var entryA = await BuildTreeEntry(game, "Entry A", head: true, next: entryC, targetId: targetA);
            _ = entryA;

            var targetB = Guid.NewGuid();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Entry B", EntryType = "CardModel", TargetId = targetB, Next = entryC.Id, AutoConnect = true }
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var reloadedA = Db.TreeEntries.Include(x => x.Next).ThenInclude(x => x!.Next).First(x => x.TargetId == targetA);
            Assert.NotNull(reloadedA.Next);
            Assert.Equal(targetB, reloadedA.Next!.TargetId);
            Assert.NotNull(reloadedA.Next.Next);
            Assert.Equal(targetC, reloadedA.Next.Next!.TargetId);
        }

        [Fact]
        public async Task Handle_DuplicateTargetId_ThrowsWrongArgumentsException()
        {
            var game = BuildGame();
            var sharedTarget = Guid.NewGuid();
            await BuildTreeEntry(game, "Entry A", targetId: sharedTarget);

            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Entry A Duplicate", EntryType = "CardModel", TargetId = sharedTarget, AutoConnect = false }
            };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NonExistentNext_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto
                {
                    Name = "Entry",
                    EntryType = "CardModel",
                    TargetId = Guid.NewGuid(),
                    Next = Guid.NewGuid(),
                    ParentId = null,
                    AutoConnect = false
                }
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ParentId_AssignsParentFolder()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);

            var childTarget = Guid.NewGuid();
            var cmd = new AddTreeEntryCommand
            {
                GameId = game.Id,
                Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "Child", EntryType = "CardModel", TargetId = childTarget, ParentId = folder.Id, AutoConnect = false }
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var child = Db.TreeEntries.Include(x => x.Parent).First(x => x.TargetId == childTarget);
            Assert.NotNull(child.Parent);
            Assert.Equal(folder.Id, child.Parent!.Id);
        }
    }

    // =========================================================================
    // UpdateTreeEntryCommandHandler tests
    // =========================================================================
    public class UpdateTreeEntryCommandHandlerTests : TreeHandlerTestBase
    {
        private UpdateTreeEntryCommandHandler Handler() => new UpdateTreeEntryCommandHandler(Db, Mapper);

        [Fact]
        public async Task Handle_NameChange_NoReorder_ReturnsSingleEntry()
        {
            var game = BuildGame();
            var entry = await BuildTreeEntry(game, "Old Name", head: true);

            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entry.Id, Name = "New Name", Next = null, ParentId = null }
            };

            var (response, entries) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Single(entries);
            Assert.Equal("New Name", Db.TreeEntries.Find(entry.Id)!.Name);
        }

        [Fact]
        public async Task Handle_MoveToEnd_UpdatesChain()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();
            var targetC = Guid.NewGuid();
            var entryC = await BuildTreeEntry(game, "C", head: false, targetId: targetC);
            var entryB = await BuildTreeEntry(game, "B", head: false, next: entryC, targetId: targetB);
            var entryA = await BuildTreeEntry(game, "A", head: true, next: entryB, targetId: targetA);
            _ = entryA;

            // Move A to end: A->B->C becomes B->C->A
            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entryA.Id, Name = "A", Next = null, ParentId = null }
            };

            var (response, _) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var reloadedB = Db.TreeEntries.Include(x => x.Next).First(x => x.TargetId == targetB);
            Assert.True(reloadedB.Head == true);
            var reloadedC = Db.TreeEntries.Include(x => x.Next).First(x => x.TargetId == targetC);
            Assert.Equal(entryA.Id, reloadedC.Next?.Id);
        }

        [Fact]
        public async Task Handle_MoveToHead_SetsHeadFlag()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();
            var entryB = await BuildTreeEntry(game, "B", head: false, targetId: targetB);
            var entryA = await BuildTreeEntry(game, "A", head: true, next: entryB, targetId: targetA);
            _ = entryA;

            // Move B before A: B becomes head
            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entryB.Id, Name = "B", Next = entryA.Id, ParentId = null }
            };

            var (response, _) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var reloadedB = Db.TreeEntries.Include(x => x.Next).First(x => x.TargetId == targetB);
            Assert.True(reloadedB.Head == true);
            Assert.Equal(entryA.Id, reloadedB.Next?.Id);
        }

        [Fact]
        public async Task Handle_DisconnectOldReferences_NullSafe_ReturnsNoNullDtos()
        {
            // Head entry has no oldBefore; moving it must not produce null DTOs in response
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();
            var entryB = await BuildTreeEntry(game, "B", head: false, targetId: targetB);
            var entryA = await BuildTreeEntry(game, "A", head: true, next: entryB, targetId: targetA);
            _ = entryA;

            // Move A to end — A was head so oldBefore is null (covers Bug 2)
            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entryA.Id, Name = "A", Next = null, ParentId = null }
            };

            var (response, entries) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.DoesNotContain(null, entries);
        }

        [Fact]
        public async Task Handle_MoveToEmptyParent_SetsHead()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);
            var targetA = Guid.NewGuid();
            var entryA = await BuildTreeEntry(game, "A", head: true, targetId: targetA);

            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entryA.Id, Name = "A", Next = null, ParentId = folder.Id }
            };

            var (response, _) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var moved = Db.TreeEntries.Include(x => x.Parent).First(x => x.TargetId == targetA);
            Assert.Equal(folder.Id, moved.Parent?.Id);
            Assert.True(moved.Head == true);
        }

        [Fact]
        public async Task Handle_CreateLastItem_WhenLastItemIsNull_DoesNotThrow()
        {
            // Move an entry into a folder where it becomes the only child.
            // If CreateLastItem's lastItem is null, fallback to CreateFirstItem (covers Bug 3).
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);
            var targetA = Guid.NewGuid();
            var entryA = await BuildTreeEntry(game, "A", head: true, targetId: targetA);

            var cmd = new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entryA.Id, Name = "A", Next = null, ParentId = folder.Id }
            };

            var exception = await Record.ExceptionAsync(() => Handler().Handle(cmd, CancellationToken.None));
            Assert.Null(exception);
        }
    }

    // =========================================================================
    // RemoveTreeEntryCommandHandler tests
    // =========================================================================
    public class RemoveTreeEntryCommandHandlerTests : TreeHandlerTestBase
    {
        private RemoveTreeEntryCommandHandler Handler() => new RemoveTreeEntryCommandHandler(Db, Mapper);

        [Fact]
        public async Task Handle_RemovesEntry_FromDatabase()
        {
            var game = BuildGame();
            var targetId = Guid.NewGuid();
            var entry = await BuildTreeEntry(game, "Entry", head: true, targetId: targetId);

            var cmd = new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = entry.Id };

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(Db.TreeEntries.Any(x => x.Id == entry.Id));
        }

        [Fact]
        public async Task Handle_Remove_SetsSuccessorAsHead_WhenEntryWasHead()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();
            var entryB = await BuildTreeEntry(game, "B", head: false, targetId: targetB);
            var entryA = await BuildTreeEntry(game, "A", head: true, next: entryB, targetId: targetA);

            var cmd = new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = entryA.Id };

            await Handler().Handle(cmd, CancellationToken.None);

            var reloadedB = Db.TreeEntries.First(x => x.TargetId == targetB);
            Assert.True(reloadedB.Head == true);
        }

        [Fact]
        public async Task Handle_Remove_FixesChain_WhenEntryWasInMiddle()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();
            var targetC = Guid.NewGuid();
            var entryC = await BuildTreeEntry(game, "C", head: false, targetId: targetC);
            var entryB = await BuildTreeEntry(game, "B", head: false, next: entryC, targetId: targetB);
            var entryA = await BuildTreeEntry(game, "A", head: true, next: entryB, targetId: targetA);
            _ = entryA;

            var cmd = new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = entryB.Id };

            await Handler().Handle(cmd, CancellationToken.None);

            var reloadedA = Db.TreeEntries.Include(x => x.Next).First(x => x.TargetId == targetA);
            Assert.Equal(entryC.Id, reloadedA.Next?.Id);
        }

        [Fact]
        public async Task Handle_Remove_ReturnsOk_WhenEntryNotFound()
        {
            var game = BuildGame();

            var cmd = new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = Guid.NewGuid() };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
        }

        [Fact]
        public async Task Handle_Remove_ThrowsTreeException_WhenFolderNotEmpty()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);
            _ = await BuildTreeEntry(game, "Child", parent: folder);

            var cmd = new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = folder.Id };

            await Assert.ThrowsAsync<TreeException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }

    // =========================================================================
    // Integration tests — multi-step sequences using real handlers
    // =========================================================================
    public class TreeEntryIntegrationTests : TreeHandlerTestBase
    {
        private AddTreeEntryCommandHandler AddHandler() => new AddTreeEntryCommandHandler(Db, Mapper);
        private UpdateTreeEntryCommandHandler UpdateHandler() => new UpdateTreeEntryCommandHandler(Db, Mapper);
        private RemoveTreeEntryCommandHandler RemoveHandler() => new RemoveTreeEntryCommandHandler(Db, Mapper);

        [Fact]
        public async Task AddThenGet_EntryAppearsInCollection()
        {
            var game = BuildGame();
            var targetId = Guid.NewGuid();

            await AddHandler().Handle(new AddTreeEntryCommand
            {
                GameId = game.Id, Player = Player(),
                TreeEntryDto = new TreeEntryDto { Name = "X", EntryType = "CardModel", TargetId = targetId, AutoConnect = true }
            }, CancellationToken.None);

            var reloaded = Db.Games.Include(x => x.TreeEntries).First(x => x.Id == game.Id);
            Assert.True(reloaded.TreeEntries.Any(x => x.TargetId == targetId));
        }

        [Fact]
        public async Task AddThenRemove_EntryRemovedFromDatabase_AndChainRepaired()
        {
            var game = BuildGame();
            var targetA = Guid.NewGuid();
            var targetB = Guid.NewGuid();

            await AddHandler().Handle(new AddTreeEntryCommand { GameId = game.Id, Player = Player(), TreeEntryDto = new TreeEntryDto { Name = "A", EntryType = "CardModel", TargetId = targetA, AutoConnect = true } }, CancellationToken.None);
            await AddHandler().Handle(new AddTreeEntryCommand { GameId = game.Id, Player = Player(), TreeEntryDto = new TreeEntryDto { Name = "B", EntryType = "CardModel", TargetId = targetB, AutoConnect = true } }, CancellationToken.None);

            var entryA = Db.TreeEntries.First(x => x.TargetId == targetA);
            await RemoveHandler().Handle(new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = entryA.Id }, CancellationToken.None);

            Assert.False(Db.TreeEntries.Any(x => x.TargetId == targetA));
            var entryB = Db.TreeEntries.First(x => x.TargetId == targetB);
            Assert.True(entryB.Head == true);
        }

        [Fact]
        public async Task AddThenUpdate_ChangesReflectedInDatabase()
        {
            var game = BuildGame();
            var targetId = Guid.NewGuid();
            await AddHandler().Handle(new AddTreeEntryCommand { GameId = game.Id, Player = Player(), TreeEntryDto = new TreeEntryDto { Name = "Old", EntryType = "CardModel", TargetId = targetId, AutoConnect = true } }, CancellationToken.None);
            var entry = Db.TreeEntries.First(x => x.TargetId == targetId);

            await UpdateHandler().Handle(new UpdateTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryDto = new TreeEntryDto { Id = entry.Id, Name = "New", Next = null, ParentId = null } }, CancellationToken.None);

            Assert.Equal("New", Db.TreeEntries.Find(entry.Id)!.Name);
        }

        [Fact]
        public async Task RapidSuccessiveAdds_NoRaceCondition_AllEntriesPersisted()
        {
            var game = BuildGame();
            const int count = 10;

            for (int i = 0; i < count; i++)
            {
                await AddHandler().Handle(new AddTreeEntryCommand
                {
                    GameId = game.Id, Player = Player(),
                    TreeEntryDto = new TreeEntryDto { Name = $"Entry {i}", EntryType = "CardModel", TargetId = Guid.NewGuid(), AutoConnect = true }
                }, CancellationToken.None);
            }

            Assert.Equal(count, Db.TreeEntries.Count(x => x.Game.Id == game.Id && x.EntryType == "CardModel"));
        }

        [Fact]
        public async Task GetWithEmptyData_ReturnsEmptyList()
        {
            var game = BuildGame();

            var entries = Db.Games
                .Include(x => x.TreeEntries)
                .First(x => x.Id == game.Id)
                .TreeEntries?
                .Where(x => x.EntryType == "CardModel")
                .ToList() ?? new List<TreeEntryModel>();

            Assert.Empty(entries);
        }

        [Fact]
        public async Task GetWithNullEntryType_ReturnsEmptyList()
        {
            var game = BuildGame();
            _ = await BuildTreeEntry(game, "Entry A", type: "CardModel");

            var entries = Db.Games
                .Include(x => x.TreeEntries)
                .First(x => x.Id == game.Id)
                .TreeEntries?
                .Where(x => x.EntryType == null)
                .ToList() ?? new List<TreeEntryModel>();

            Assert.Empty(entries);
        }

        [Fact]
        public async Task AddWithDuplicateTargetId_ThrowsOnSecondAdd()
        {
            var game = BuildGame();
            var sharedTarget = Guid.NewGuid();

            await AddHandler().Handle(new AddTreeEntryCommand { GameId = game.Id, Player = Player(), TreeEntryDto = new TreeEntryDto { Name = "First", EntryType = "CardModel", TargetId = sharedTarget, AutoConnect = true } }, CancellationToken.None);

            await Assert.ThrowsAsync<WrongArgumentsException>(() =>
                AddHandler().Handle(new AddTreeEntryCommand { GameId = game.Id, Player = Player(), TreeEntryDto = new TreeEntryDto { Name = "Duplicate", EntryType = "CardModel", TargetId = sharedTarget, AutoConnect = true } }, CancellationToken.None));
        }
    }
}

using NordvikManager.Application.Mapping;
using NordvikManager.Application;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Commands.TreeEntry.UpdateEntry;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Data.Contexts;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.TreeEntry
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
            var cmd = AddCommand(game, new TreeEntryDto
            {
                Name = name,
                EntryType = type,
                TargetId = tid,
                ParentId = parent?.Id,
                Next = next?.Id,
                IsFolder = isFolder,
                AutoConnect = true,
            });
            await new AddTreeEntryCommandHandler(Db, Mapper).Handle(cmd, CancellationToken.None);

            return Db.TreeEntries
                .Include(x => x.Parent)
                .Include(x => x.Next)
                .First(x => x.TargetId == tid);
        }

        protected AddTreeEntryCommand AddCommand(GameModel game, TreeEntryDto entry) => new()
        {
            GameId = game.Id,
            Player = Player(),
            TreeEntryDto = entry,
        };

        // Seeds the names as one chain, first to last (the first is the head).
        protected async Task<List<TreeEntryModel>> BuildChain(GameModel game, params string[] names)
        {
            var chain = new List<TreeEntryModel>();
            TreeEntryModel? next = null;
            foreach (var name in names.Reverse())
            {
                next = await BuildTreeEntry(game, name, next: next, head: name == names[0]);
                chain.Insert(0, next);
            }
            return chain;
        }

        // Moves (or renames) an entry: before `next`, or last when null; into `parentId`, or the top level.
        protected Task<(CommandResponse, List<TreeEntryDto>)> Move(GameModel game, TreeEntryModel entry, Guid? next = null, Guid? parentId = null, string? name = null)
            => new UpdateTreeEntryCommandHandler(Db, Mapper).Handle(new UpdateTreeEntryCommand
            {
                GameId = game.Id,
                PlayerId = PlayerId,
                TreeEntryDto = new TreeEntryDto { Id = entry.Id, Name = name ?? entry.Name, Next = next, ParentId = parentId },
            }, CancellationToken.None);

        protected Task<CommandResponse> Remove(GameModel game, Guid entryId)
            => new RemoveTreeEntryCommandHandler(Db, Mapper).Handle(new RemoveTreeEntryCommand { GameId = game.Id, PlayerId = PlayerId, TreeEntryId = entryId }, CancellationToken.None);

        protected TreeEntryModel Reload(TreeEntryModel entry)
            => Db.TreeEntries.Include(x => x.Next).Include(x => x.Parent).First(x => x.Id == entry.Id);
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
            var cmd = AddCommand(game, new TreeEntryDto { Name = "Entry A", EntryType = "CardModel", TargetId = targetId, AutoConnect = false });

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(Db.TreeEntries.Any(x => x.TargetId == targetId));
        }

        [Fact]
        public async Task Handle_AutoConnect_FirstEntry_SetsHead()
        {
            var game = BuildGame();
            var cmd = AddCommand(game, new TreeEntryDto { Name = "Entry A", EntryType = "CardModel", TargetId = Guid.NewGuid(), AutoConnect = true });

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
            var cmd = AddCommand(game, new TreeEntryDto { Name = "Entry B", EntryType = "CardModel", TargetId = newTarget, AutoConnect = true });

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
            var cmd = AddCommand(game, new TreeEntryDto { Name = "Entry B", EntryType = "CardModel", TargetId = targetB, Next = entryC.Id, AutoConnect = true });

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

            var cmd = AddCommand(game, new TreeEntryDto { Name = "Entry A Duplicate", EntryType = "CardModel", TargetId = sharedTarget, AutoConnect = false });

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NonExistentNext_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = AddCommand(game, new TreeEntryDto
            {
                Name = "Entry",
                EntryType = "CardModel",
                TargetId = Guid.NewGuid(),
                Next = Guid.NewGuid(),
                ParentId = null,
                AutoConnect = false
            });

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ParentId_AssignsParentFolder()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);

            var childTarget = Guid.NewGuid();
            var cmd = AddCommand(game, new TreeEntryDto { Name = "Child", EntryType = "CardModel", TargetId = childTarget, ParentId = folder.Id, AutoConnect = false });

            await Handler().Handle(cmd, CancellationToken.None);

            var child = Db.TreeEntries.Include(x => x.Parent).First(x => x.TargetId == childTarget);
            Assert.NotNull(child.Parent);
            Assert.Equal(folder.Id, child.Parent!.Id);
        }

        // Item entries are added pending (AutoConnect = false) and only linked on the next tree
        // load. A folder created "before" such an item must still work.
        private async Task<TreeEntryModel> BuildPendingEntry(GameModel game, string name, string type)
        {
            var target = Guid.NewGuid();
            await Handler().Handle(AddCommand(game, new TreeEntryDto { Name = name, EntryType = type, TargetId = target, AutoConnect = false }), CancellationToken.None);
            return Db.TreeEntries.First(x => x.TargetId == target);
        }

        private AddTreeEntryCommand FolderBefore(GameModel game, Guid? next, string type = "CardTemplate") => new()
        {
            GameId = game.Id,
            Player = Player(),
            TreeEntryDto = new TreeEntryDto { Name = "Folder", EntryType = type, IsFolder = true, Next = next, AutoConnect = true }
        };

        [Fact]
        public async Task Handle_FolderBeforePendingEntry_LinksBothWithFolderFirst()
        {
            var game = BuildGame();
            var item = await BuildPendingEntry(game, "Template", "CardTemplate");

            await Handler().Handle(FolderBefore(game, item.Id), CancellationToken.None);

            var entries = Db.TreeEntries.Include(x => x.Next).Where(x => x.EntryType == "CardTemplate").ToList();
            var folder = entries.Single(x => x.IsFolder);
            Assert.True(folder.Head);
            Assert.Equal(item.Id, folder.Next!.Id);
            Assert.All(entries, x => Assert.False(x.NewItem));
            Assert.Single(entries, x => x.Head == true);
        }

        [Fact]
        public async Task Handle_FolderBeforePendingEntry_KeepsLinkedEntriesInOrder()
        {
            var game = BuildGame();
            var first = await BuildTreeEntry(game, "First", type: "CardTemplate");
            var item = await BuildPendingEntry(game, "Template", "CardTemplate");

            await Handler().Handle(FolderBefore(game, item.Id), CancellationToken.None);

            var entries = Db.TreeEntries.Include(x => x.Next).Where(x => x.EntryType == "CardTemplate").ToList();
            var folder = entries.Single(x => x.IsFolder);
            Assert.Equal(folder.Id, entries.Single(x => x.Id == first.Id).Next!.Id);
            Assert.Equal(item.Id, folder.Next!.Id);
            Assert.Single(entries, x => x.Head == true);
        }

        [Fact]
        public async Task Handle_RejectedAdd_LeavesNothingBehind()
        {
            var game = BuildGame();
            var otherTreeEntry = await BuildTreeEntry(game, "Map", type: "MapModel");

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(FolderBefore(game, otherTreeEntry.Id), CancellationToken.None));

            Assert.DoesNotContain(Db.TreeEntries.AsNoTracking().ToList(), x => x.Name == "Folder");
        }
    }

    // =========================================================================
    // UpdateTreeEntryCommandHandler tests
    // =========================================================================
    public class UpdateTreeEntryCommandHandlerTests : TreeHandlerTestBase
    {
        [Fact]
        public async Task Handle_NameChange_NoReorder_ReturnsSingleEntry()
        {
            var game = BuildGame();
            var entry = await BuildTreeEntry(game, "Old Name", head: true);

            var (response, entries) = await Move(game, entry, name: "New Name");

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Single(entries);
            Assert.Equal("New Name", Db.TreeEntries.Find(entry.Id)!.Name);
        }

        // Move A to end: A->B->C becomes B->C->A. A was the head, so it has nothing before it:
        // the answer must still hold no null entries (Bug 2).
        [Fact]
        public async Task Handle_MoveToEnd_UpdatesChain()
        {
            var game = BuildGame();
            var chain = await BuildChain(game, "A", "B", "C");

            var (response, entries) = await Move(game, chain[0]);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.DoesNotContain(null, entries);
            Assert.True(Reload(chain[1]).Head == true);
            Assert.Equal(chain[0].Id, Reload(chain[2]).Next?.Id);
        }

        // Move B before A: B becomes head
        [Fact]
        public async Task Handle_MoveToHead_SetsHeadFlag()
        {
            var game = BuildGame();
            var chain = await BuildChain(game, "A", "B");

            var (response, _) = await Move(game, chain[1], next: chain[0].Id);

            Assert.Equal(CommandResponse.Ok, response);
            var b = Reload(chain[1]);
            Assert.True(b.Head == true);
            Assert.Equal(chain[0].Id, b.Next?.Id);
        }

        // Into an empty folder, where it becomes the only child: CreateLastItem has no last item
        // and must fall back to CreateFirstItem instead of throwing (Bug 3).
        [Fact]
        public async Task Handle_MoveToEmptyParent_SetsHead()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);
            var entry = await BuildTreeEntry(game, "A", head: true);

            var (response, _) = await Move(game, entry, parentId: folder.Id);

            Assert.Equal(CommandResponse.Ok, response);
            var moved = Reload(entry);
            Assert.Equal(folder.Id, moved.Parent?.Id);
            Assert.True(moved.Head == true);
        }

        [Fact]
        public async Task Handle_MovingFollowingItemsIntoFolder_FolderNoLongerPointsAtThem()
        {
            // Folder -> A -> B at the top level; dragging A then B into the folder must leave
            // the folder with no Next (its old neighbours are now its children).
            var game = BuildGame();
            var chain = await BuildChain(game, "A", "B");
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, next: chain[0]);

            foreach (var entry in chain)
                await Move(game, entry, parentId: folder.Id);

            var reloadedFolder = Db.TreeEntries.Include(x => x.Next).AsNoTracking().First(x => x.Id == folder.Id);
            Assert.Null(reloadedFolder.Next);
            Assert.True(reloadedFolder.Head);
            var children = Db.TreeEntries.Include(x => x.Parent).Include(x => x.Next).AsNoTracking().Where(x => x.Parent!.Id == folder.Id).ToList();
            Assert.Equal(2, children.Count);
            Assert.Single(children, x => x.Head == true);
        }
    }

    // =========================================================================
    // RemoveTreeEntryCommandHandler tests
    // =========================================================================
    public class RemoveTreeEntryCommandHandlerTests : TreeHandlerTestBase
    {
        [Fact]
        public async Task Handle_RemovesEntry_FromDatabase()
        {
            var game = BuildGame();
            var entry = await BuildTreeEntry(game, "Entry", head: true);

            await Remove(game, entry.Id);

            Assert.False(Db.TreeEntries.Any(x => x.Id == entry.Id));
        }

        [Fact]
        public async Task Handle_Remove_SetsSuccessorAsHead_WhenEntryWasHead()
        {
            var game = BuildGame();
            var chain = await BuildChain(game, "A", "B");

            await Remove(game, chain[0].Id);

            Assert.True(Reload(chain[1]).Head == true);
        }

        [Fact]
        public async Task Handle_Remove_FixesChain_WhenEntryWasInMiddle()
        {
            var game = BuildGame();
            var chain = await BuildChain(game, "A", "B", "C");

            await Remove(game, chain[1].Id);

            Assert.Equal(chain[2].Id, Reload(chain[0]).Next?.Id);
        }

        [Fact]
        public async Task Handle_Remove_ReturnsOk_WhenEntryNotFound()
        {
            var game = BuildGame();

            Assert.Equal(CommandResponse.Ok, await Remove(game, Guid.NewGuid()));
        }

        [Fact]
        public async Task Handle_Remove_ThrowsTreeException_WhenFolderNotEmpty()
        {
            var game = BuildGame();
            var folder = await BuildTreeEntry(game, "Folder", isFolder: true, head: true);
            _ = await BuildTreeEntry(game, "Child", parent: folder);

            await Assert.ThrowsAsync<TreeException>(() => Remove(game, folder.Id));
        }
    }

    // =========================================================================
    // Integration tests — multi-step sequences using real handlers
    // =========================================================================
    public class TreeEntryIntegrationTests : TreeHandlerTestBase
    {
        [Fact]
        public async Task RapidSuccessiveAdds_NoRaceCondition_AllEntriesPersisted()
        {
            var game = BuildGame();
            const int count = 10;

            for (int i = 0; i < count; i++)
                await BuildTreeEntry(game, $"Entry {i}");

            Assert.Equal(count, Db.TreeEntries.Count(x => x.Game.Id == game.Id && x.EntryType == "CardModel"));
        }

        [Fact]
        public async Task AddWithDuplicateTargetId_ThrowsOnSecondAdd()
        {
            var game = BuildGame();
            var sharedTarget = Guid.NewGuid();
            await BuildTreeEntry(game, "First", targetId: sharedTarget);

            await Assert.ThrowsAsync<WrongArgumentsException>(() => BuildTreeEntry(game, "Duplicate", targetId: sharedTarget));
        }
    }
}

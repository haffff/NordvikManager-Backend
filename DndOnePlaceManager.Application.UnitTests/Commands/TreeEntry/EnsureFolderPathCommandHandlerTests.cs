using DndOnePlaceManager.Application.Commands.TreeEntry.EnsureFolderPath;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    // A folder path ("Lore/Trilium") in one tree: found by name level by level, the missing
    // folders created. Lets an action put what it creates into a folder the GM picked.
    public class EnsureFolderPathCommandHandlerTests : TreeHandlerTestBase
    {
        private EnsureFolderPathCommandHandler Handler() => new(Db, Mapper);

        private Task<(Guid? FolderId, List<DataTransferObjects.TreeEntryDto> Created)> Ensure(GameModel game, string path, string type = "CardModel") =>
            Handler().Handle(new EnsureFolderPathCommand { GameID = game.Id, Player = Player(), EntryType = type, Path = path }, CancellationToken.None);

        [Fact]
        public async Task Handle_CreatesEachMissingFolder_AndReturnsTheLast()
        {
            var game = BuildGame();

            var (folderId, created) = await Ensure(game, "Lore / Trilium");

            Assert.Equal(new[] { "Lore", "Trilium" }, created.Select(x => x.Name));
            var trilium = Db.TreeEntries.Include(x => x.Parent).Single(x => x.Id == folderId);
            Assert.Equal("Trilium", trilium.Name);
            Assert.True(trilium.IsFolder);
            Assert.Equal("CardModel", trilium.EntryType);
            Assert.Equal("Lore", trilium.Parent!.Name);
            Assert.Null(Db.TreeEntries.Include(x => x.Parent).Single(x => x.Name == "Lore").Parent);
        }

        [Fact]
        public async Task Handle_ReusesFoldersThatExist_IgnoringCase()
        {
            var game = BuildGame();
            var lore = await BuildTreeEntry(game, "Lore", isFolder: true, head: true);

            var (folderId, created) = await Ensure(game, "lore/Trilium");
            var (again, createdAgain) = await Ensure(game, "Lore/trilium");

            Assert.Equal(new[] { "Trilium" }, created.Select(x => x.Name));
            Assert.Equal(lore.Id, Db.TreeEntries.Include(x => x.Parent).Single(x => x.Id == folderId).Parent!.Id);
            Assert.Equal(folderId, again);
            Assert.Empty(createdAgain);
            Assert.Equal(2, Db.TreeEntries.Count(x => x.IsFolder));
        }

        [Fact]
        public async Task Handle_LooksOnlyInItsOwnTree()
        {
            var game = BuildGame();
            var templates = await BuildTreeEntry(game, "Lore", type: "CardTemplate", isFolder: true, head: true);

            var (folderId, created) = await Ensure(game, "Lore");

            Assert.Single(created);
            Assert.NotEqual(templates.Id, folderId);
        }

        [Fact]
        public async Task Handle_DoesNotTakeANonFolderEntryOfThatName()
        {
            var game = BuildGame();
            var card = await BuildTreeEntry(game, "Lore", head: true);

            var (folderId, created) = await Ensure(game, "Lore");

            Assert.Single(created);
            Assert.NotEqual(card.Id, folderId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData(" / /")]
        [InlineData(null)]
        public async Task Handle_NoPath_IsTheTopLevel(string? path)
        {
            var game = BuildGame();

            var (folderId, created) = await Ensure(game, path!);

            Assert.Null(folderId);
            Assert.Empty(created);
            Assert.False(Db.TreeEntries.Any());
        }

        [Fact]
        public async Task Handle_RefusesTooDeepOrTooLongPaths()
        {
            var game = BuildGame();

            await Assert.ThrowsAnyAsync<Exception>(() => Ensure(game, string.Join("/", Enumerable.Range(0, 11).Select(i => "f" + i))));
            await Assert.ThrowsAnyAsync<Exception>(() => Ensure(game, new string('x', 101)));
            Assert.False(Db.TreeEntries.Any());
        }

        [Fact]
        public async Task Handle_NeedsEditOnTheGame()
        {
            var game = BuildGame();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.IsAny<IEntity>(), Permission.Edit)).Returns(false);

            await Assert.ThrowsAsync<PermissionException>(() => Ensure(game, "Lore"));
            Assert.False(Db.TreeEntries.Any());
        }
    }
}

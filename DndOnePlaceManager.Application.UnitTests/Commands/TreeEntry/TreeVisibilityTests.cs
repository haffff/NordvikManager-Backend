using DndOnePlaceManager.Application.Commands.TreeEntry.GetTreeEntries;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TreeEntry
{
    // A player who isn't a GM gets only the entries of what they may see, and the folders on
    // the way to them: entries carry names (a card's title), and empty folders are clutter.
    // Siblings are linked again around what's left out, so the order stays the GM's.
    public class TreeVisibilityTests : TreeHandlerTestBase
    {
        private readonly GameModel game;
        private readonly HashSet<Guid> readable = new();
        // Setting up needs Edit (making folders); the tree is then loaded as a player, or a GM.
        private bool isGm = true;

        public TreeVisibilityTests()
        {
            game = BuildGame();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.Is<IEntity>(e => e is GameModel), Permission.Edit))
                .Returns(() => isGm);
            PermissionsMock.Setup(p => p.GetPermittedIds(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), Permission.Read))
                .Returns((Guid _, IEnumerable<Guid> ids, Permission _) => ids.Where(readable.Contains).ToHashSet());
        }

        private async Task<List<TreeEntryDto>> Load(string type = "CardModel", bool asGm = false)
        {
            isGm = asGm;
            Db.ChangeTracker.Clear();
            return await new GetTreeEntriesCommandHandler(Db, Mapper, new Mock<IMediator>().Object, PermissionsMock.Object)
                .Handle(new GetTreeEntriesCommand { GameId = game.Id, PlayerId = PlayerId, EntityType = type }, CancellationToken.None);
        }

        private async Task<TreeEntryModel> Card(string name, TreeEntryModel? folder = null, bool shared = false)
        {
            var entry = await BuildTreeEntry(game, name, parent: folder);
            if (shared) readable.Add(entry.TargetId!.Value);
            return entry;
        }

        private Task<TreeEntryModel> Folder(string name, TreeEntryModel? parent = null, string type = "CardModel") =>
            BuildTreeEntry(game, name, type: type, parent: parent, isFolder: true, targetId: Guid.NewGuid());

        [Fact]
        public async Task APlayer_GetsWhatTheyMaySee_AndTheFoldersLeadingThere()
        {
            var lore = await Folder("Lore");
            var secrets = await Folder("Secrets");
            await Folder("Empty");
            var deep = await Folder("Deep", lore);
            await Card("Hidden note", lore);
            await Card("Shared note", lore, shared: true);
            await Card("Secret", secrets);
            await Card("Deep secret", deep);
            await Card("Shared at the top", shared: true);

            var names = (await Load()).Select(x => x.Name).ToList();

            Assert.Equal(new[] { "Lore", "Shared note", "Shared at the top" }.OrderBy(x => x), names.OrderBy(x => x));
        }

        [Fact]
        public async Task APlayer_GetsSiblingsLinkedAroundWhatIsLeftOut()
        {
            var lore = await Folder("Lore");
            await Folder("Empty");
            await Card("Hidden", lore);
            await Card("First", lore, shared: true);
            await Card("Also hidden", lore);
            await Card("Second", lore, shared: true);
            await Card("At the top", shared: true);

            var tree = await Load();
            TreeEntryDto Named(string n) => tree.Single(x => x.Name == n);

            // Top level: Lore, (Empty), At the top
            Assert.True(Named("Lore").Head);
            Assert.Equal(Named("At the top").Id, Named("Lore").Next);
            Assert.Null(Named("At the top").Next);
            Assert.NotEqual(true, Named("At the top").Head);
            // In Lore: (Hidden), First, (Also hidden), Second
            Assert.True(Named("First").Head);
            Assert.Equal(Named("Second").Id, Named("First").Next);
            Assert.Null(Named("Second").Next);
        }

        [Fact]
        public async Task AGm_GetsTheWholeTree()
        {
            var lore = await Folder("Lore");
            await Folder("Empty");
            await Card("Hidden note", lore);

            Assert.Equal(3, (await Load(asGm: true)).Count);
        }

        [Fact]
        public async Task APlayer_GetsTheirOwnResources_NotOthers()
        {
            var mine = Guid.NewGuid();
            var theirs = Guid.NewGuid();
            var other = new PlayerModel { Id = Guid.NewGuid(), Name = "Other" };
            Db.Players.Add(other);
            Db.Resources.Add(new ResourceModel { Id = mine, GameId = game.Id, PlayerId = PlayerId, Name = "mine.png" });
            Db.Resources.Add(new ResourceModel { Id = theirs, GameId = game.Id, PlayerId = other.Id, Name = "theirs.png" });
            Db.SaveChanges();
            var folder = await Folder("Uploads", type: "ResourceModel");
            await BuildTreeEntry(game, "mine.png", type: "ResourceModel", parent: folder, targetId: mine);
            await BuildTreeEntry(game, "theirs.png", type: "ResourceModel", targetId: theirs);

            var names = (await Load("ResourceModel")).Select(x => x.Name).OrderBy(x => x);

            Assert.Equal(new[] { "Uploads", "mine.png" }.OrderBy(x => x), names);
        }

        [Theory]
        [InlineData("Playlist")]
        [InlineData("Soundboard")]
        public async Task APlayer_GetsNoPlaylists(string type)
        {
            var folder = await Folder("Music", type: type);
            var track = await BuildTreeEntry(game, "Battle", type: type, parent: folder);
            readable.Add(track.TargetId!.Value);

            Assert.Empty(await Load(type));
        }
    }
}

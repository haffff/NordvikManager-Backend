using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.BattleMap;
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

        // ── Backfill: items made before their panel had folders get an entry on load ──

        private CardModel SeedCard(GameModel game, string name, bool template = false, bool customUi = false)
        {
            var card = new CardModel
            {
                Id = Guid.NewGuid(), Name = name, GameId = game.Id, Game = game,
                IsTemplate = template, IsCustomUi = customUi, Properties = new List<PropertyModel>(),
            };
            Db.Cards.Add(card);
            Db.SaveChanges();
            return card;
        }

        private PlaylistModel SeedPlaylist(GameModel game, string name, PlaylistKind kind)
        {
            var playlist = new PlaylistModel { Id = Guid.NewGuid(), GameId = game.Id, Name = name, Description = "", Kind = kind };
            Db.Playlists.Add(playlist);
            Db.SaveChanges();
            return playlist;
        }

        private Task Load(GameModel game, string entityType) =>
            Handler().Handle(new ConnectTreeEntriesCommand { GameID = game.Id, EntityType = entityType }, CancellationToken.None);

        private List<TreeEntryModel> Entries(string entityType) =>
            Db.TreeEntries.Include(x => x.Next).Where(x => x.EntryType == entityType).ToList();

        [Fact]
        public async Task Handle_TemplateTree_CreatesLinkedEntryForEachTemplateWithoutOne()
        {
            var game = BuildGame();
            var template = SeedCard(game, "Goblin template", template: true);
            SeedCard(game, "Regular card");
            SeedCard(game, "Some view", customUi: true);

            await Load(game, "CardTemplate");

            var entry = Assert.Single(Entries("CardTemplate"));
            Assert.Equal(template.Id, entry.TargetId);
            Assert.Equal("Goblin template", entry.Name);
            Assert.False(entry.IsFolder);
            Assert.False(entry.NewItem);
            Assert.True(entry.Head);
        }

        [Fact]
        public async Task Handle_CustomViewTree_OnlyPicksCustomUiCards()
        {
            var game = BuildGame();
            var view = SeedCard(game, "Initiative view", customUi: true);
            SeedCard(game, "Goblin template", template: true);

            await Load(game, "CustomView");

            Assert.Equal(view.Id, Assert.Single(Entries("CustomView")).TargetId);
        }

        [Theory]
        [InlineData("Playlist", PlaylistKind.Music, PlaylistKind.Soundboard)]
        [InlineData("Soundboard", PlaylistKind.Soundboard, PlaylistKind.Music)]
        public async Task Handle_PlaylistTrees_PickPlaylistsOfTheirKind(string entityType, PlaylistKind kind, PlaylistKind otherKind)
        {
            var game = BuildGame();
            var mine = SeedPlaylist(game, "Mine", kind);
            SeedPlaylist(game, "Other", otherKind);

            await Load(game, entityType);

            Assert.Equal(mine.Id, Assert.Single(Entries(entityType)).TargetId);
        }

        [Fact]
        public async Task Handle_MissingItems_AreChainedInOneList()
        {
            var game = BuildGame();
            SeedCard(game, "A", template: true);
            SeedCard(game, "B", template: true);

            await Load(game, "CardTemplate");

            var entries = Entries("CardTemplate");
            Assert.Equal(2, entries.Count);
            var head = Assert.Single(entries, e => e.Head == true);
            Assert.NotNull(head.Next);
            Assert.Contains(entries, e => e.Id == head.Next!.Id && e.Next == null);
        }

        [Fact]
        public async Task Handle_LoadingTwice_DoesNotDuplicateEntries()
        {
            var game = BuildGame();
            SeedCard(game, "A", template: true);

            await Load(game, "CardTemplate");
            await Load(game, "CardTemplate");

            Assert.Single(Entries("CardTemplate"));
        }

        [Fact]
        public async Task Handle_ItemAlreadyFiled_IsLeftWhereItIs()
        {
            var game = BuildGame();
            var template = SeedCard(game, "A", template: true);
            var folder = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "Monsters", IsFolder = true, EntryType = "CardTemplate", Head = true, NewItem = false };
            var filed = new TreeEntryModel { Id = Guid.NewGuid(), Game = game, Name = "A", TargetId = template.Id, Parent = folder, EntryType = "CardTemplate", Head = true, NewItem = false };
            Db.TreeEntries.AddRange(folder, filed);
            Db.SaveChanges();

            await Load(game, "CardTemplate");

            var entries = Db.TreeEntries.Include(x => x.Parent).Where(x => x.EntryType == "CardTemplate").ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(folder.Id, entries.Single(e => e.TargetId == template.Id).Parent!.Id);
        }

        [Fact]
        public async Task Handle_CardTree_IsNotBackfilled()
        {
            // Regular cards already get entries when created; the card tree keeps its behaviour.
            var game = BuildGame();
            SeedCard(game, "Regular card");

            await Load(game, "CardModel");

            Assert.Empty(Entries("CardModel"));
        }

        [Fact]
        public async Task Handle_IgnoresItemsOfOtherGames()
        {
            var game = BuildGame();
            var otherGame = new GameModel { Id = Guid.NewGuid(), Name = "Other Game", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            Db.Games.Add(otherGame);
            Db.SaveChanges();
            SeedCard(otherGame, "Not mine", template: true);

            await Load(game, "CardTemplate");

            Assert.Empty(Entries("CardTemplate"));
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

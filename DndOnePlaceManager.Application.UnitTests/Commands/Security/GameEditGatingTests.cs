using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Player.RemovePlayer;
using DndOnePlaceManager.Application.Commands.Playlist.GetPlaylists;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Resources.CreateResource;
using DndOnePlaceManager.Application.Commands.Resources.GetResource;
using DndOnePlaceManager.Application.Commands.Resources.Link;
using DndOnePlaceManager.Application.Commands.Resources.Transfer;
using DndOnePlaceManager.Application.Commands.Resoures;
using DndOnePlaceManager.Application.Commands.Security.SetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Security
{
    // Who may do what in a game, over real permission rows: the owner (All), an editor
    // (Edit on the game), a reader (Read) and a player with no row of their own (falls back
    // to the "everyone" row). Editing follows the game's Edit permission; the host's disk
    // (linking, browsing, host paths) is the local admin's alone.
    public class GameEditGatingTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid otherGameId = Guid.NewGuid();
        private readonly Guid systemId = Guid.NewGuid();
        private readonly Guid editorId = Guid.NewGuid();
        private readonly Guid readerId = Guid.NewGuid();
        private readonly Guid newcomerId = Guid.NewGuid();
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid otherMapId = Guid.NewGuid();
        private readonly Guid editorResourceId = Guid.NewGuid();
        private readonly Guid readerResourceId = Guid.NewGuid();
        private readonly Mock<IFileStorageProvider> storage = new();

        // PlayerId (from the base) is the owner.
        public GameEditGatingTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = gameId, Name = "Campaign", MasterId = PlayerId, SystemPlayerId = systemId,
                Players = new List<PlayerModel>
                {
                    new PlayerModel { Id = PlayerId, Name = "Owner", CentralServerUserId = "owner" },
                    new PlayerModel { Id = systemId, Name = "System", System = true },
                    new PlayerModel { Id = editorId, Name = "Editor", CentralServerUserId = "editor" },
                    new PlayerModel { Id = readerId, Name = "Reader", CentralServerUserId = "reader" },
                    new PlayerModel { Id = newcomerId, Name = "Newcomer", CentralServerUserId = "newcomer" },
                },
                Maps = new List<MapModel> { new MapModel { Id = mapId, Name = "Dungeon" } },
            });
            seed.Games.Add(new GameModel
            {
                Id = otherGameId, Name = "Other", SystemPlayerId = Guid.NewGuid(),
                Players = new List<PlayerModel>(),
                Maps = new List<MapModel> { new MapModel { Id = otherMapId, Name = "Elsewhere" } },
            });
            seed.Permissions.AddRange(
                Row(gameId, PlayerId, Permission.All),
                Row(gameId, systemId, Permission.All),
                Row(gameId, editorId, Permission.Read | Permission.Edit),
                Row(gameId, readerId, Permission.Read),
                new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = Guid.Empty, All = true, Permission = Permission.Read | Permission.Execute },
                Row(mapId, PlayerId, Permission.All),
                Row(mapId, editorId, Permission.All));
            seed.Resources.AddRange(
                new ResourceModel { Id = editorResourceId, GameId = gameId, PlayerId = editorId, Name = "map.png", Path = "C:/host/map.png", Storage = ResourceStorageKind.Linked, Data = Array.Empty<byte>() },
                new ResourceModel { Id = readerResourceId, GameId = gameId, PlayerId = readerId, Name = "token.png", Data = new byte[] { 1, 2 } });
            seed.SaveChanges();

            storage.Setup(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<string?>())).ReturnsAsync("C:/managed/file");
            storage.Setup(s => s.ListDirectory(It.IsAny<string?>())).Returns(new List<LocalDirectoryEntry>());
        }

        private static PermissionModel Row(Guid model, Guid player, Permission permission) =>
            new() { Id = Guid.NewGuid(), ModelID = model, PlayerID = player, Permission = permission };

        private static PlayerDTO As(Guid id) => new() { Id = id };

        // ── The game permission the client gets ─────────────────────────────────

        [Theory]
        [InlineData("editor", Permission.Read | Permission.Edit)]
        [InlineData("reader", Permission.Read)]
        [InlineData("newcomer", Permission.Read | Permission.Execute)] // the "everyone" row
        public async Task GetPlayer_PermissionIsThePlayersPermissionOnTheGame(string userId, Permission expected)
        {
            var result = await new GetPlayerCommandHandler(Db, Mapper).Handle(
                new GetPlayerCommand { GameID = gameId, User = new User { Id = userId } }, CancellationToken.None);

            Assert.Equal(expected, result.Player!.Permission);
        }

        // ── Resources: the list follows Edit, host paths follow local admin ─────

        [Fact]
        public async Task GetResources_EditorSeesEveryonesResources_ReaderOnlyTheirOwn()
        {
            var handler = new GetResourcesCommandHandler(Db, Mapper);

            var (_, editorSees) = await handler.Handle(new GetResourcesCommand { GameId = gameId, Player = As(editorId) }, CancellationToken.None);
            var (_, readerSees) = await handler.Handle(new GetResourcesCommand { GameId = gameId, Player = As(readerId) }, CancellationToken.None);

            Assert.Equal(2, editorSees.Count);
            Assert.Equal("token.png", Assert.Single(readerSees).Name);
        }

        [Fact]
        public async Task GetResources_HostPathOnlyForTheLocalAdmin()
        {
            var handler = new GetResourcesCommandHandler(Db, Mapper);

            var (_, owner) = await handler.Handle(new GetResourcesCommand { GameId = gameId, Player = As(PlayerId) }, CancellationToken.None);
            var (_, admin) = await handler.Handle(new GetResourcesCommand { GameId = gameId, Player = As(editorId), IsLocalAdmin = true }, CancellationToken.None);

            Assert.Null(owner.Single(x => x.Name == "map.png").Path);
            Assert.Equal("C:/host/map.png", admin.Single(x => x.Name == "map.png").Path);
        }

        [Fact]
        public async Task GetResource_HostPathOnlyForTheLocalAdmin()
        {
            var handler = new GetResourceCommandHandler(Db, Mapper);

            var owner = await handler.Handle(new GetResourceCommand { ResourceId = editorResourceId, Player = As(PlayerId) }, CancellationToken.None);
            var admin = await handler.Handle(new GetResourceCommand { ResourceId = editorResourceId, Player = As(readerId), IsLocalAdmin = true }, CancellationToken.None);

            Assert.Null(owner.Path);
            Assert.Equal("C:/host/map.png", admin.Path);
        }

        [Fact]
        public async Task GetPlaylists_HostPathOnlyForTheLocalAdmin()
        {
            using (var seed = SeedContext())
            {
                seed.Playlists.Add(new PlaylistModel
                {
                    Id = Guid.NewGuid(), GameId = gameId, Name = "Battle", Description = "",
                    Resources = new List<ResourceModel> { seed.Resources.Find(editorResourceId)! },
                });
                seed.SaveChanges();
            }
            var handler = new GetPlaylistsCommandHandler(Db, Mapper, Permissions);

            var owner = await handler.Handle(new GetPlaylistsCommand { GameId = gameId, Player = As(PlayerId) }, CancellationToken.None);
            var admin = await handler.Handle(new GetPlaylistsCommand { GameId = gameId, Player = As(editorId), IsLocalAdmin = true }, CancellationToken.None);

            Assert.Null(Assert.Single(Assert.Single(owner).Resources).Path);
            Assert.Equal("C:/host/map.png", Assert.Single(Assert.Single(admin).Resources).Path);
        }

        // ── The host's disk: linking and browsing are the local admin's ─────────

        [Fact]
        public async Task BrowseLocalDirectory_OwnerWhoIsNotLocalAdmin_IsRefused()
        {
            var handler = new BrowseLocalDirectoryCommandHandler(Db, Mapper, storage.Object);

            await Assert.ThrowsAsync<PermissionException>(() =>
                handler.Handle(new BrowseLocalDirectoryCommand { GameId = gameId, Player = As(PlayerId) }, CancellationToken.None));
        }

        [Fact]
        public async Task BrowseLocalDirectory_LocalAdmin_IsAllowed()
        {
            var handler = new BrowseLocalDirectoryCommandHandler(Db, Mapper, storage.Object);

            var entries = await handler.Handle(new BrowseLocalDirectoryCommand { GameId = gameId, Player = As(readerId), IsLocalAdmin = true }, CancellationToken.None);

            Assert.Empty(entries);
        }

        [Fact]
        public async Task LinkResource_OwnerWhoIsNotLocalAdmin_IsRefused()
        {
            var handler = new LinkResourceCommandHandler(Db, Mapper, Mock.Of<IMediator>(), storage.Object);

            await Assert.ThrowsAsync<PermissionException>(() =>
                handler.Handle(new LinkResourceCommand { GameId = gameId, Player = As(PlayerId), Name = "x", LocalPath = "C:/x.png" }, CancellationToken.None));
        }

        [Fact]
        public async Task LinkDirectory_OwnerWhoIsNotLocalAdmin_IsRefused()
        {
            var handler = new LinkDirectoryCommandHandler(Db, Mapper, Mock.Of<IMediator>());

            await Assert.ThrowsAsync<PermissionException>(() =>
                handler.Handle(new LinkDirectoryCommand { GameId = gameId, Player = As(PlayerId), LocalDirectoryPath = "C:/" }, CancellationToken.None));
        }

        // ── Storing in the server's own folder follows Edit ─────────────────────

        [Fact]
        public async Task TransferResourceStorage_Editor_IsAllowed_Reader_IsRefused()
        {
            var handler = new TransferResourceStorageCommandHandler(Db, Mapper, storage.Object);

            var result = await handler.Handle(new TransferResourceStorageCommand { GameId = gameId, Player = As(editorId), ResourceId = readerResourceId, TargetStorage = ResourceStorageKind.ManagedFile }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            await Assert.ThrowsAsync<PermissionException>(() =>
                handler.Handle(new TransferResourceStorageCommand { GameId = gameId, Player = As(readerId), ResourceId = readerResourceId, TargetStorage = ResourceStorageKind.Blob }, CancellationToken.None));
        }

        [Fact]
        public async Task CreateResource_OnDisk_Editor_IsAllowed_Reader_IsRefused()
        {
            var handler = new CreateResourceCommandHandler(Db, Mapper, Mock.Of<IMediator>(), storage.Object);
            CreateResourceCommand Create(Guid player) => new()
            {
                GameId = gameId, Player = As(player), Name = "a.png", Data = new byte[] { 1 }, MimeType = "image/png", StorageKind = ResourceStorageKind.ManagedFile,
            };

            var (response, _) = await handler.Handle(Create(editorId), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            await Assert.ThrowsAsync<PermissionException>(() => handler.Handle(Create(readerId), CancellationToken.None));
        }

        // ── Game permissions stay the owner's ───────────────────────────────────

        private Task<CommandResponse> SetPermissions(Guid caller, string entityType, Guid entityId, Guid target, Permission permission) =>
            new SetPermissionsCommandHandler(Db, Mapper).Handle(new SetPermissionsCommand
            {
                GameID = gameId, Player = As(caller), EntityType = entityType, EntityId = entityId,
                Permissions = new Dictionary<Guid, Permission?> { [target] = permission },
            }, CancellationToken.None);

        private Permission? StoredPermission(Guid model, Guid player) =>
            SeedContext().Permissions.AsNoTracking().FirstOrDefault(x => x.ModelID == model && x.PlayerID == player)?.Permission;

        [Fact]
        public async Task SetPermissions_EditorOnAMap_IsAllowed()
        {
            await SetPermissions(editorId, "MapModel", mapId, readerId, Permission.Read | Permission.Control);

            Assert.Equal(Permission.Read | Permission.Control, StoredPermission(mapId, readerId));
        }

        [Fact]
        public async Task SetPermissions_EditorOnTheGame_IsRefused_OwnerIsAllowed()
        {
            await Assert.ThrowsAsync<PermissionException>(() => SetPermissions(editorId, "GameModel", gameId, editorId, Permission.All));

            await SetPermissions(PlayerId, "GameModel", gameId, readerId, Permission.Read | Permission.Edit);
            Assert.Equal(Permission.Read | Permission.Edit, StoredPermission(gameId, readerId));
        }

        // Ignored rather than refused: copying or grouping elements resends the whole list,
        // owner's row included.
        [Fact]
        public async Task SetPermissions_EditorChangingTheOwnersOrSystemRow_IsIgnored()
        {
            await SetPermissions(editorId, "MapModel", mapId, PlayerId, Permission.None);
            await SetPermissions(editorId, "MapModel", mapId, systemId, Permission.None);

            Assert.Equal(Permission.All, StoredPermission(mapId, PlayerId));
            Assert.Null(StoredPermission(mapId, systemId));
        }

        // ── Players: editors manage them, but not the owner ─────────────────────

        [Fact]
        public async Task RemovePlayer_EditorRemovesAReader_ButNotTheOwner()
        {
            var handler = new RemovePlayerCommandHandler(Db, Mapper);

            await handler.Handle(new RemovePlayerCommand { GameID = gameId, Player = As(editorId), PlayerID = readerId }, CancellationToken.None);
            await Assert.ThrowsAsync<PermissionException>(() =>
                handler.Handle(new RemovePlayerCommand { GameID = gameId, Player = As(editorId), PlayerID = PlayerId }, CancellationToken.None));

            using var check = SeedContext();
            Assert.False(check.Players.Any(x => x.Id == readerId));
            Assert.True(check.Players.Any(x => x.Id == PlayerId));
        }

        // Each kind the permission UI sets, when it belongs to this game.
        [Fact]
        public async Task SetPermissions_EachEntityKindOfThisGame_IsAllowed()
        {
            var elementId = Guid.NewGuid();
            var cardId = Guid.NewGuid();
            var layoutId = Guid.NewGuid();
            var propertyId = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                seed.Elements.Add(new ElementModel { Id = elementId, Map = seed.Maps.Find(mapId)! });
                seed.Cards.Add(new CardModel { Id = cardId, Name = "Hero", GameId = gameId });
                seed.Layouts.Add(new LayoutModel { Id = layoutId, Name = "Main", Value = "{}", GameModelId = gameId });
                seed.SaveChanges();
                seed.Properties.Add(new PropertyModel { Id = propertyId, Name = "hp", Value = "1", ParentID = cardId, EntityName = "CardModel", Card = seed.Cards.Find(cardId)! });
                seed.SaveChanges();
            }

            foreach (var (type, id) in new[] { ("ElementModel", elementId), ("CardModel", cardId), ("LayoutModel", layoutId), ("PropertyModel", propertyId) })
            {
                await SetPermissions(editorId, type, id, readerId, Permission.Read);
                Assert.Equal(Permission.Read, StoredPermission(id, readerId));
            }
        }

        [Fact]
        public async Task SetPermissions_OnAnotherGamesEntity_IsRefused()
        {
            await Assert.ThrowsAsync<PermissionException>(() => SetPermissions(PlayerId, "MapModel", otherMapId, readerId, Permission.All));

            Assert.Null(StoredPermission(otherMapId, readerId));
        }
    }
}

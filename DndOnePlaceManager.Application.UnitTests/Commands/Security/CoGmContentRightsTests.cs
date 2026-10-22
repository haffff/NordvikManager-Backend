using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Player.GetGamePlayers;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.Security.GetEffectivePermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Security
{
    // A player with Edit on the game (a co-GM) has, on everything in that game, at least
    // what its owner has — addon GM content included, restrictions included — and nothing
    // in other games. Chat history is the exception: whispers stay private.
    public class CoGmContentRightsTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId = Guid.NewGuid();
        private readonly Guid systemId = Guid.NewGuid();
        private readonly Guid editorId = Guid.NewGuid();
        private readonly Guid readerId = Guid.NewGuid();
        private readonly Guid ownersMapId = Guid.NewGuid();
        private readonly Guid gmCardId = Guid.NewGuid();
        private readonly Guid otherGameId = Guid.NewGuid();
        private readonly Guid otherOwnerId = Guid.NewGuid();
        private readonly Guid otherMapId = Guid.NewGuid();

        // PlayerId (from the base) owns the game.
        public CoGmContentRightsTests()
        {
            using var seed = SeedContext();
            seed.Games.AddRange(
                new GameModel
                {
                    Id = gameId, Name = "Campaign", MasterId = PlayerId, SystemPlayerId = systemId,
                    Players = new List<PlayerModel>
                    {
                        new PlayerModel { Id = PlayerId, Name = "Owner" },
                        new PlayerModel { Id = systemId, Name = "System", System = true },
                        new PlayerModel { Id = editorId, Name = "Editor" },
                        new PlayerModel { Id = readerId, Name = "Reader" },
                    },
                    Maps = new List<MapModel> { new MapModel { Id = ownersMapId, Name = "Dungeon" } },
                    Cards = new List<CardModel> { new CardModel { Id = gmCardId, Name = "Monster Template" } },
                },
                new GameModel
                {
                    Id = otherGameId, Name = "Other", MasterId = otherOwnerId, SystemPlayerId = Guid.NewGuid(),
                    Players = new List<PlayerModel> { new PlayerModel { Id = otherOwnerId, Name = "Them" } },
                    Maps = new List<MapModel> { new MapModel { Id = otherMapId, Name = "Elsewhere" } },
                });
            seed.Permissions.AddRange(
                Row(gameId, PlayerId, Permission.All),
                Row(gameId, systemId, Permission.All),
                Row(gameId, editorId, Permission.Read | Permission.Edit),
                Row(gameId, readerId, Permission.Read),
                // The owner's map: rows for the owner and the system only.
                Row(ownersMapId, PlayerId, Permission.All),
                Row(ownersMapId, systemId, Permission.All),
                // Addon GM content: the addon gave the GM Read only on this hidden template.
                Row(gmCardId, PlayerId, Permission.Read),
                Row(otherMapId, otherOwnerId, Permission.All));
            seed.SaveChanges();
        }

        private static PermissionModel Row(Guid model, Guid player, Permission permission) =>
            new() { Id = Guid.NewGuid(), ModelID = model, PlayerID = player, Permission = permission };

        private bool Can(Guid player, Guid model, Permission permission) =>
            Permissions.CheckIfHasPermissions(new PlayerDTO { Id = player }, model, permission);

        // ── The permission service ──────────────────────────────────────────────

        [Fact]
        public void Editor_HasTheOwnersRightsOnTheOwnersMap_ReaderHasNone()
        {
            Assert.True(Can(editorId, ownersMapId, Permission.Edit));
            Assert.Equal(Permission.All, Permissions.GetPermission(editorId, ownersMapId));

            Assert.False(Can(readerId, ownersMapId, Permission.Read));
        }

        [Fact]
        public void Editor_GetsAddonGmContent_WithTheSameRestriction()
        {
            Assert.True(Can(editorId, gmCardId, Permission.Read));
            Assert.False(Can(editorId, gmCardId, Permission.Edit));
            Assert.Contains(gmCardId, Permissions.GetPermittedIds(editorId, new[] { gmCardId, ownersMapId }, Permission.Read));
            Assert.Empty(Permissions.GetPermittedIds(readerId, new[] { gmCardId, ownersMapId }, Permission.Read));
        }

        [Fact]
        public void Editor_DoesNotInheritTheOwnersRightsOnTheGameItself()
        {
            Assert.False(Can(editorId, gameId, Permission.Remove));
            Assert.Equal(Permission.Read | Permission.Edit, Permissions.GetPermission(editorId, gameId));
            Assert.Empty(Permissions.GetPermittedIds(editorId, new[] { gameId }, Permission.Remove));
        }

        [Fact]
        public void Editor_GetsNothingInAnotherGame()
        {
            Assert.False(Can(editorId, otherMapId, Permission.Read));
            Assert.Null(Permissions.GetPermission(editorId, otherMapId));
            Assert.Empty(Permissions.GetPermittedIds(editorId, new[] { otherMapId }, Permission.Read));
        }

        [Fact]
        public void OwnRowStillCounts_OnTopOfTheOwners()
        {
            using (var seed = SeedContext())
            {
                seed.Permissions.Add(Row(gmCardId, editorId, Permission.Execute));
                seed.SaveChanges();
            }

            Assert.Equal(Permission.Read | Permission.Execute, Permissions.GetPermission(editorId, gmCardId));
        }

        // ── Content a co-GM creates is the owner's too ──────────────────────────

        [Fact]
        public async Task AddMap_ByAnEditor_GivesTheOwnerAllOnIt()
        {
            var handler = new AddMapCommandHandler(Db, Mapper, Mock.Of<IMediator>());

            var (_, mapId) = await handler.Handle(new AddMapCommand { GameID = gameId, Player = new PlayerDTO { Id = editorId }, Dto = new MapDTO { Name = "Cave" } }, CancellationToken.None);

            using var check = SeedContext();
            Assert.Equal(Permission.All, check.Permissions.Single(x => x.ModelID == mapId && x.PlayerID == PlayerId).Permission);
        }

        // ── Live updates ────────────────────────────────────────────────────────

        [Fact]
        public async Task EffectivePermissions_IncludeEditorsWithTheOwnersBits()
        {
            var result = await new GetEffectivePermissionsCommandHandler(Db, Mapper).Handle(
                new GetEffectivePermissionsCommand { GameId = gameId, EntityId = gmCardId }, CancellationToken.None);

            Assert.Equal(Permission.Read, result[editorId]);
            Assert.False(result.ContainsKey(readerId));
        }

        // ── Players list: each with their permission on the game (chat routes /gm by it) ──

        [Fact]
        public async Task GetGamePlayers_GivesEachPlayersGamePermission()
        {
            var players = await new GetGamePlayersCommandHandler(Db, Mapper).Handle(new GetGamePlayersCommand { GameId = gameId }, CancellationToken.None);

            Assert.Equal(Permission.Read | Permission.Edit, players.Single(p => p.Id == editorId).Permission);
            Assert.Equal(Permission.Read, players.Single(p => p.Id == readerId).Permission);
            Assert.True(players.Single(p => p.Id == PlayerId).IsOwner);
        }

        // ── Chat history: whispers stay private ─────────────────────────────────

        // Whispers stay private everywhere, not only in the history query: no permission
        // check on a message adds the owner's row for a co-GM.
        [Fact]
        public async Task Whisper_NoPermissionCheckGivesItToACoGm()
        {
            var whisperId = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                seed.Messages.Add(new MessageModel { Id = whisperId, GameId = gameId, PlayerId = readerId, Content = "psst", Created = DateTime.UtcNow });
                seed.Permissions.AddRange(Row(whisperId, readerId, Permission.Read), Row(whisperId, PlayerId, Permission.Read));
                seed.SaveChanges();
            }

            Assert.False(Can(editorId, whisperId, Permission.Read));
            Assert.Null(Permissions.GetPermission(editorId, whisperId));
            Assert.Empty(Permissions.GetPermittedIds(editorId, new[] { whisperId }, Permission.Read));
            var effective = await new GetEffectivePermissionsCommandHandler(Db, Mapper).Handle(
                new GetEffectivePermissionsCommand { GameId = gameId, EntityId = whisperId }, CancellationToken.None);
            Assert.False(effective.ContainsKey(editorId));
        }

        [Fact]
        public async Task GetMessages_EditorDoesNotSeeAWhisperBetweenOthers()
        {
            var whisperId = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                seed.Messages.Add(new MessageModel { Id = whisperId, GameId = gameId, PlayerId = readerId, Content = "psst", Created = DateTime.UtcNow });
                seed.Permissions.AddRange(Row(whisperId, readerId, Permission.Read), Row(whisperId, PlayerId, Permission.Read));
                seed.SaveChanges();
            }

            var seen = await new GetMessagesCommandHandler(Db, Mapper).Handle(
                new GetMessagesCommand { GameID = gameId, PlayerID = editorId, Size = 10 }, CancellationToken.None);

            Assert.Empty(seen);
        }

        // ── Resources ───────────────────────────────────────────────────────────

        [Fact]
        public async Task RemoveResource_EditorRemovesAnotherPlayersResource()
        {
            var resourceId = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                seed.Resources.Add(new ResourceModel { Id = resourceId, GameId = gameId, PlayerId = readerId, Name = "a.png", Data = new byte[] { 1 } });
                seed.SaveChanges();
            }

            await new RemoveResourceCommandHandler(Db, Mapper, Mock.Of<IMediator>(), Mock.Of<IFileStorageProvider>()).Handle(new RemoveResourceCommand
            {
                ID = resourceId, GameId = gameId, Player = new PlayerDTO { Id = editorId, Permission = Permission.Read | Permission.Edit },
            }, CancellationToken.None);

            using var check = SeedContext();
            Assert.False(check.Resources.Any(x => x.Id == resourceId));
        }
    }
}

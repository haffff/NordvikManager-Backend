using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.DeleteGame;
using NordvikManager.Application.Commands.Game.RemoveGame;
using NordvikManager.Application.Commands.Game.UpdateGame;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace NordvikManager.Application.UnitTests.Commands.Game
{
    // The game list: a player sees the games they are in and manages those they own; the
    // server's local admin sees and manages every game, member or not.
    public class LobbyGameManagementTests : SqliteHandlerTestBase
    {
        private readonly Guid ownGameId = Guid.NewGuid();
        private readonly Guid joinedGameId = Guid.NewGuid();
        private readonly Guid strangersGameId = Guid.NewGuid();
        private readonly Guid strangerId = Guid.NewGuid();

        // PlayerId (from the base) is "me" in my own game; I'm a plain player in another.
        public LobbyGameManagementTests()
        {
            using var seed = SeedContext();
            var meInJoined = Guid.NewGuid();
            seed.Games.AddRange(
                new GameModel
                {
                    Id = ownGameId, Name = "Mine", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(), Password = "secret", UseMetric = true,
                    Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM", CentralServerUserId = "me" } },
                },
                new GameModel
                {
                    Id = joinedGameId, Name = "Joined", MasterId = Guid.NewGuid(), SystemPlayerId = Guid.NewGuid(),
                    Players = new List<PlayerModel> { new PlayerModel { Id = meInJoined, Name = "Me", CentralServerUserId = "me" } },
                },
                new GameModel
                {
                    Id = strangersGameId, Name = "Theirs", MasterId = strangerId, SystemPlayerId = Guid.NewGuid(),
                    Players = new List<PlayerModel> { new PlayerModel { Id = strangerId, Name = "Them", CentralServerUserId = "them" } },
                    Properties = new List<PropertyModel> { new PropertyModel { Id = Guid.NewGuid(), Name = "shortDescription", Value = "old" } },
                });
            seed.Permissions.AddRange(
                new PermissionModel { Id = Guid.NewGuid(), ModelID = ownGameId, PlayerID = PlayerId, Permission = Permission.All },
                new PermissionModel { Id = Guid.NewGuid(), ModelID = joinedGameId, PlayerID = meInJoined, Permission = Permission.Read | Permission.Edit },
                new PermissionModel { Id = Guid.NewGuid(), ModelID = strangersGameId, PlayerID = strangerId, Permission = Permission.All },
                new PermissionModel { Id = Guid.NewGuid(), ModelID = strangersGameId, PlayerID = Guid.Empty, All = true, Permission = Permission.Read });
            seed.SaveChanges();
        }

        private Task<GetGameListCommandResponse> List(bool allGames) =>
            new GetGameListCommandHandler(Db, Mapper).Handle(new GetGameListCommand { UserId = "me", AllGames = allGames }, CancellationToken.None);

        // ── Listing ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task List_Player_SeesTheirGames_ManagesOnlyTheOwnedOne()
        {
            var games = (await List(allGames: false)).GameItemList;

            Assert.Equal(new[] { "Joined", "Mine" }, games.Select(x => x.Name).OrderBy(x => x));
            Assert.All(games, x => Assert.True(x.IsMember));
            Assert.True(games.Single(x => x.Name == "Mine").CanManage);
            Assert.False(games.Single(x => x.Name == "Joined").CanManage);
        }

        [Fact]
        public async Task List_LocalAdmin_SeesAndManagesEveryGame()
        {
            var games = (await List(allGames: true)).GameItemList;

            Assert.Equal(new[] { "Joined", "Mine", "Theirs" }, games.Select(x => x.Name).OrderBy(x => x));
            Assert.All(games, x => Assert.True(x.CanManage));
            var theirs = games.Single(x => x.Name == "Theirs");
            Assert.False(theirs.IsMember);
            Assert.False(theirs.IsOwner);
        }

        // ── Deleting ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Remove_LocalAdmin_DeletesAGameTheyAreNotIn_WithAllItsPermissionRows()
        {
            await new RemoveGameCommandHandler(Db, Mapper).Handle(new RemoveGameCommand { GameID = strangersGameId, AsLocalAdmin = true }, CancellationToken.None);

            using var check = SeedContext();
            Assert.False(check.Games.Any(x => x.Id == strangersGameId));
            Assert.False(check.Permissions.Any(x => x.ModelID == strangersGameId));
        }

        [Fact]
        public async Task Remove_Owner_DeletesTheirGame()
        {
            await new RemoveGameCommandHandler(Db, Mapper).Handle(new RemoveGameCommand { GameID = ownGameId, Player = new PlayerDTO { Id = PlayerId } }, CancellationToken.None);

            using var check = SeedContext();
            Assert.False(check.Games.Any(x => x.Id == ownGameId));
        }

        [Fact]
        public async Task Remove_NotLocalAdminAndNoPlayer_IsRefused()
        {
            await Assert.ThrowsAsync<PermissionException>(() =>
                new RemoveGameCommandHandler(Db, Mapper).Handle(new RemoveGameCommand { GameID = strangersGameId }, CancellationToken.None));
        }

        // ── Renaming ────────────────────────────────────────────────────────────

        [Fact]
        public async Task Update_LocalAdmin_RenamesAGameTheyAreNotIn_AndSetsItsDescription()
        {
            await new UpdateGameCommandHandler(Db, Mapper).Handle(new UpdateGameCommand
            {
                GameId = strangersGameId, AsLocalAdmin = true, Name = "Renamed", ShortDescription = "new",
            }, CancellationToken.None);

            using var check = SeedContext();
            var game = check.Games.Include(x => x.Properties).Single(x => x.Id == strangersGameId);
            Assert.Equal("Renamed", game.Name);
            Assert.Equal("new", Assert.Single(game.Properties!, p => p.Name == "shortDescription").Value);
        }

        [Fact]
        public async Task Update_NameOnly_KeepsPasswordAndUnits()
        {
            await new UpdateGameCommandHandler(Db, Mapper).Handle(new UpdateGameCommand
            {
                GameId = ownGameId, Player = new PlayerDTO { Id = PlayerId }, Name = "Renamed",
            }, CancellationToken.None);

            using var check = SeedContext();
            var game = check.Games.Single(x => x.Id == ownGameId);
            Assert.Equal("secret", game.Password);
            Assert.True(game.UseMetric);
        }

        [Fact]
        public async Task Update_NotLocalAdminAndNoPlayer_IsRefused()
        {
            await Assert.ThrowsAsync<PermissionException>(() =>
                new UpdateGameCommandHandler(Db, Mapper).Handle(new UpdateGameCommand { GameId = strangersGameId, Name = "x" }, CancellationToken.None));
        }
    }
}

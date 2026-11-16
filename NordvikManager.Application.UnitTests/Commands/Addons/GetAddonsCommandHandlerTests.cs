using NordvikManager.Application.Commands.Addons.GetAddons;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    public class GetAddonsCommandHandlerTests : HandlerTestBase
    {
        private GetAddonsCommandHandler Handler() => new(Db, Mapper);

        private AddonModel SeedAddon(NordvikManager.Domain.Entities.BattleMap.GameModel game, string key)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key, Version = "1.0" };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

        // Known pre-existing bug: game.Addons is dereferenced before the permission check
        // and before a null-check on game — pinning down current behavior, not fixing it.
        [Fact]
        public async Task Handle_GameNotFound_ThrowsNullReferenceException_KnownBug()
        {
            var cmd = new GetAddonsCommand { GameId = Guid.NewGuid(), Player = Player() };

            await Assert.ThrowsAsync<NullReferenceException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ReturnsEmptyList()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_ValidRequest_ReturnsMappedAddons()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            SeedAddon(game, "pathfinder");
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Handle_FlatTrue_ReturnsFlatDtosWithLimitedFields()
        {
            var game = BuildGame();
            SeedAddon(game, "dnd5e");
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player(), Flat = true };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("dnd5e", result[0].Key);
            Assert.Equal("1.0", result[0].Version);
        }

        // The Installed tab shows them.
        [Fact]
        public async Task Handle_FlatTrue_IncludesDependencies()
        {
            var game = BuildGame();
            var addon = SeedAddon(game, "dnd5e");
            addon.Dependencies = new List<AddonDependency> { new() { Key = "core", Version = "2.0" } };
            Db.SaveChanges();
            var cmd = new GetAddonsCommand { GameId = game.Id, Player = Player(), Flat = true };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            var dependency = Assert.Single(result[0].Dependencies!);
            Assert.Equal("core", dependency.Key);
            Assert.Equal("2.0", dependency.Version);
        }

        private NordvikManager.Domain.Entities.BattleMap.ActionModel Action(NordvikManager.Domain.Entities.BattleMap.GameModel game, string name, string content)
        {
            var action = new NordvikManager.Domain.Entities.BattleMap.ActionModel { Id = Guid.NewGuid(), Name = name, Prefix = "addon", Game = game, Content = content };
            Db.Actions.Add(action);
            return action;
        }

        // Where an addon connects (info.json "network"), and whether its actions make outside
        // requests at all (those don't work on the browser version), in both list shapes.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Handle_IncludesNetwork_AndWhetherItsActionsGoOutside(bool flat)
        {
            var game = BuildGame();
            var notes = SeedAddon(game, "notes");
            notes.Network = new AddonNetwork { Hosts = new() { "https://api.example.com" }, UserHosts = "Your server" };
            notes.Actions = new() { Action(game, "Fetch", "[{\"Type\":\"SendRequest\",\"Data\":{}}]") };
            var poll = SeedAddon(game, "poll");
            poll.Actions = new() { Action(game, "Vote", "[{\"Type\":\"SendChat\",\"Data\":{}}]") };
            Db.SaveChanges();

            var result = await Handler().Handle(new GetAddonsCommand { GameId = game.Id, Player = Player(), Flat = flat }, CancellationToken.None);

            var notesDto = result.Single(a => a.Key == "notes");
            Assert.Equal(new[] { "https://api.example.com" }, notesDto.Network!.Hosts);
            Assert.Equal("Your server", notesDto.Network.UserHosts);
            Assert.True(notesDto.UsesOutsideConnections);
            var pollDto = result.Single(a => a.Key == "poll");
            Assert.Null(pollDto.Network);
            Assert.False(pollDto.UsesOutsideConnections);
        }

        // Only a step of that type counts (anywhere, e.g. in an If's branch), not the word
        // in a message or a variable name.
        [Fact]
        public async Task Handle_UsesOutsideConnections_OnlyForSuchSteps()
        {
            var game = BuildGame();
            var chat = SeedAddon(game, "chat");
            chat.Actions = new() { Action(game, "Say", "[{\"Type\":\"SendChat\",\"Data\":{\"Message\":\"SendRequest or DownloadResource\",\"Output\":\"lastSendRequest\"}}]") };
            var nested = SeedAddon(game, "nested");
            nested.Actions = new() { Action(game, "Maybe", "[{\"Type\":\"If\",\"Data\":{\"Then\":[{\"type\":\"DownloadResource\",\"Data\":{}}]}}]") };
            Db.SaveChanges();

            var result = await Handler().Handle(new GetAddonsCommand { GameId = game.Id, Player = Player(), Flat = true }, CancellationToken.None);

            Assert.False(result.Single(a => a.Key == "chat").UsesOutsideConnections);
            Assert.True(result.Single(a => a.Key == "nested").UsesOutsideConnections);
        }
    }
}

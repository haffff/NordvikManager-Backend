using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.Commands.Addons.UninstallAddon;
using NordvikManager.Application.Commands.Card;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Interfaces;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using MediatR;
using Moq;
using GameModel = NordvikManager.Domain.Entities.BattleMap.GameModel;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    public class UninstallAddonCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IGameEventLogger> _logger = new();

        private UninstallAddonCommandHandler Handler() => new(Db, _mediator.Object, Mapper, _logger.Object);

        private AddonModel SeedAddon(Guid gameId, string key = "dnd5e")
        {
            var addon = new AddonModel
            {
                Id = Guid.NewGuid(), Name = "DnD 5e", Key = key, Version = "1.0",
                Views = new List<NordvikManager.Domain.Entities.BattleMap.CardModel>(),
                Templates = new List<NordvikManager.Domain.Entities.BattleMap.CardModel>(),
                Actions = new List<NordvikManager.Domain.Entities.BattleMap.ActionModel>(),
                Resources = new List<NordvikManager.Domain.Entities.Resources.ResourceModel>(),
            };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = gameId;
            Db.SaveChanges();
            return addon;
        }

        private Task<(CommandResponse, UninstallAddonCommandResponse)> Uninstall(Guid gameId, Guid? addonId = null, string? key = null, bool forReinstall = false) =>
            Handler().Handle(new UninstallAddonCommand { GameID = gameId, Player = Player(), AddonId = addonId, AddonKey = key, ForReinstall = forReinstall }, CancellationToken.None);

        private bool AddonExists(Guid id) => SeedContext().Addons.Any(a => a.Id == id);

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Handle_AddonOfAnotherGame_IsNotFoundAndStays(bool byKey)
        {
            var game = BuildGame();
            var otherGame = OtherGame();
            var otherAddon = SeedAddon(otherGame.Id, key: "dnd5e");

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => byKey ? Uninstall(game.Id, key: "dnd5e") : Uninstall(game.Id, otherAddon.Id));
            Assert.True(AddonExists(otherAddon.Id));
        }

        [Fact]
        public async Task Handle_ByKey_TakesThisGamesAddon_WhenAnotherGameHasTheSameKey()
        {
            var otherGame = OtherGame();
            var otherAddon = SeedAddon(otherGame.Id, key: "dnd5e");
            var game = BuildGame();
            var addon = SeedAddon(game.Id, key: "dnd5e");
            var (_, result) = await Uninstall(game.Id, key: "dnd5e");

            Assert.Equal(addon.Id, result.AddonId);
            Assert.True(AddonExists(otherAddon.Id));
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Uninstall(Guid.NewGuid(), Guid.NewGuid()));
        }

        [Fact]
        public async Task Handle_AddonNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Uninstall(game.Id, Guid.NewGuid()));
        }

        [Fact]
        public async Task Handle_ValidUninstall_RemovesAddonAndReturnsInfo()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var (response, result) = await Uninstall(game.Id, addon.Id);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(addon.Id, result.AddonId);
            Assert.Equal("dnd5e", result.AddonKey);
            Assert.False(Db.Addons.Any(a => a.Id == addon.Id));
        }

        [Fact]
        public async Task Handle_FoundByKey_Succeeds()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id, key: "custom-key");
            var (response, result) = await Uninstall(game.Id, key: "custom-key");

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(addon.Id, result.AddonId);
        }

        [Fact]
        public async Task Handle_AddonWithNoChildren_DoesNotSendAnySubCommands()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            await Uninstall(game.Id, addon.Id);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveCardCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveActionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AddonWithChildren_SendsRemoveCommandForEachChild()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var resource = new NordvikManager.Domain.Entities.Resources.ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = PlayerId, Name = "r" };
            var view = new NordvikManager.Domain.Entities.BattleMap.CardModel { Id = Guid.NewGuid(), Name = "v", AdditionalResources = "[]" };
            var action = new NordvikManager.Domain.Entities.BattleMap.ActionModel { Id = Guid.NewGuid(), Name = "a", Content = "[]", Prefix = "addon" };
            Db.Resources!.Add(resource);
            Db.Cards!.Add(view);
            Db.Actions!.Add(action);
            Db.SaveChanges();
            addon.Resources!.Add(resource);
            addon.Views!.Add(view);
            addon.Actions!.Add(action);
            Db.SaveChanges();

            await Uninstall(game.Id, addon.Id);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveCardCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveActionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Mirrors just enough of RemoveTreeEntryCommandHandler's real behavior (not-empty
        // check + delete) for these tests to exercise UninstallAddonCommandHandler's own
        // folder-cleanup logic in isolation, without pulling in the real handler's
        // Next/Head linked-list bookkeeping, which isn't relevant here.
        private void SetupRealisticTreeEntryRemoval() =>
            _mediator.Setup(m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((RemoveTreeEntryCommand cmd, CancellationToken _) =>
                {
                    var entry = Db.TreeEntries.FirstOrDefault(x => x.Id == cmd.TreeEntryId);
                    if (entry == null) return CommandResponse.Ok;
                    if (Db.TreeEntries.Any(x => x.Parent != null && x.Parent.Id == entry.Id))
                        throw new TreeException("Folder is not empty!");
                    Db.TreeEntries.Remove(entry);
                    Db.SaveChanges();
                    return CommandResponse.Ok;
                });

        private TreeEntryModel SeedFolder(GameModel game, string name, TreeEntryModel? parent = null, bool isFolder = true)
        {
            var folder = new TreeEntryModel
            {
                Id = Guid.NewGuid(),
                Name = name,
                IsFolder = isFolder,
                EntryType = nameof(ResourceModel),
                Parent = parent,
                Game = game,
            };
            Db.TreeEntries.Add(folder);
            Db.SaveChanges();
            return folder;
        }

        // An addon with the "Addons/<AddonName>" folders install made, and a tree removal that behaves like the real one.
        private (GameModel Game, AddonModel Addon, TreeEntryModel Root, TreeEntryModel Folder) AddonWithFolder()
        {
            SetupRealisticTreeEntryRemoval();
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var root = SeedFolder(game, "Addons");
            return (game, addon, root, SeedFolder(game, addon.Name, root));
        }

        private bool Exists(TreeEntryModel entry) => Db.TreeEntries.Any(x => x.Id == entry.Id);

        // Regression coverage from the addon install/uninstall triage: InstallAddonCommandHandler
        // .CreateFolder builds "Addons/<AddonName>/{Scripts,Resources}" on install, but nothing
        // removed those folder entries on uninstall — every uninstall left empty ghost folders
        // behind permanently.
        [Fact]
        public async Task Handle_RemovesEmptyAddonFolderStructure_AfterUninstall()
        {
            var (game, addon, addonsRoot, addonFolder) = AddonWithFolder();
            var scripts = SeedFolder(game, "Scripts", addonFolder);
            var resources = SeedFolder(game, "Resources", addonFolder);

            await Uninstall(game.Id, addon.Id);

            Assert.False(Exists(scripts));
            Assert.False(Exists(resources));
            Assert.False(Exists(addonFolder));
            Assert.False(Exists(addonsRoot));
        }

        [Fact]
        public async Task Handle_LeavesSharedAddonsRootInPlace_WhenAnotherAddonStillHasAFolderThere()
        {
            var (game, addon, addonsRoot, addonFolder) = AddonWithFolder();
            // Another addon's folder under the same shared root — untouched by this uninstall.
            SeedFolder(game, "Pathfinder", addonsRoot);

            await Uninstall(game.Id, addon.Id);

            Assert.False(Exists(addonFolder));
            Assert.True(Exists(addonsRoot), "Shared 'Addons' root should survive while another addon still has a folder under it.");
        }

        [Fact]
        public async Task Handle_LeavesAddonFolderInPlace_WhenGmAddedExtraContentToIt()
        {
            var (game, addon, addonsRoot, addonFolder) = AddonWithFolder();
            // A tree entry the addon didn't create (e.g. the GM dragged a file in here) — a real
            // file, not a folder, so it must be seeded with isFolder: false. Getting this wrong
            // previously made the test a false positive: TryRemoveEmptyAddonFoldersAsync's own
            // subfolder-sweep only ever touches IsFolder==true children, so a folder-flagged
            // "file" here got swept up and deleted as if it were an empty subfolder, leaving
            // addonFolder looking empty too.
            SeedFolder(game, "gm-added.png", addonFolder, isFolder: false);

            await Uninstall(game.Id, addon.Id);

            Assert.True(Exists(addonFolder), "Addon folder with unrelated GM content should not be deleted.");
        }

        // ---- Dependencies ----

        private AddonModel SeedDependent(Guid gameId, string key, string name, string dependsOn)
        {
            var addon = SeedAddon(gameId, key);
            addon.Name = name;
            addon.Dependencies = new List<AddonDependency> { new() { Key = dependsOn } };
            Db.SaveChanges();
            return addon;
        }

        [Fact]
        public async Task Handle_AnotherAddonDependsOnIt_IsRefusedAndStays()
        {
            var game = BuildGame();
            var core = SeedAddon(game.Id, "core");
            SeedDependent(game.Id, "dnd5e", "Fifth Edition", dependsOn: "core");
            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Uninstall(game.Id, key: "core"));

            Assert.Contains("Fifth Edition", e.Message);
            Assert.True(AddonExists(core.Id));
        }

        // An update removes the old version itself; the addons that need it still get one.
        [Fact]
        public async Task Handle_ForReinstall_IgnoresDependents()
        {
            var game = BuildGame();
            var core = SeedAddon(game.Id, "core");
            SeedDependent(game.Id, "dnd5e", "Fifth Edition", dependsOn: "core");
            await Uninstall(game.Id, key: "core", forReinstall: true);

            Assert.False(AddonExists(core.Id));
        }

        // Only this game's addons count.
        [Fact]
        public async Task Handle_DependentInAnotherGame_DoesNotBlock()
        {
            var game = BuildGame();
            var otherGame = OtherGame();
            var core = SeedAddon(game.Id, "core");
            SeedDependent(otherGame.Id, "dnd5e", "Fifth Edition", dependsOn: "core");
            await Uninstall(game.Id, key: "core");

            Assert.False(AddonExists(core.Id));
        }
    }
}

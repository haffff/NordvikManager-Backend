using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Card;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
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
                Views = new List<DndOnePlaceManager.Domain.Entities.BattleMap.CardModel>(),
                Templates = new List<DndOnePlaceManager.Domain.Entities.BattleMap.CardModel>(),
                Actions = new List<DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel>(),
                Resources = new List<DndOnePlaceManager.Domain.Entities.Resources.ResourceModel>(),
            };
            Db.Addons.Add(addon);
            Db.SaveChanges();
            return addon;
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UninstallAddonCommand { GameID = Guid.NewGuid(), Player = Player(), AddonId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AddonNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUninstall_RemovesAddonAndReturnsInfo()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

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
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonKey = "custom-key" };

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(addon.Id, result.AddonId);
        }

        [Fact]
        public async Task Handle_AddonWithNoChildren_DoesNotSendAnySubCommands()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveCardCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveActionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AddonWithChildren_SendsRemoveCommandForEachChild()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var resource = new DndOnePlaceManager.Domain.Entities.Resources.ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = PlayerId, Name = "r" };
            var view = new DndOnePlaceManager.Domain.Entities.BattleMap.CardModel { Id = Guid.NewGuid(), Name = "v", AdditionalResources = "[]" };
            var action = new DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel { Id = Guid.NewGuid(), Name = "a", Content = "[]", Prefix = "addon" };
            Db.Resources!.Add(resource);
            Db.Cards!.Add(view);
            Db.Actions!.Add(action);
            Db.SaveChanges();
            addon.Resources!.Add(resource);
            addon.Views!.Add(view);
            addon.Actions!.Add(action);
            Db.SaveChanges();

            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };
            await Handler().Handle(cmd, CancellationToken.None);

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

        private TreeEntryModel SeedFolder(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string name, TreeEntryModel? parent = null, bool isFolder = true)
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

        // Regression coverage from the addon install/uninstall triage: InstallAddonCommandHandler
        // .CreateFolder builds "Addons/<AddonName>/{Scripts,Resources}" on install, but nothing
        // removed those folder entries on uninstall — every uninstall left empty ghost folders
        // behind permanently.
        [Fact]
        public async Task Handle_RemovesEmptyAddonFolderStructure_AfterUninstall()
        {
            SetupRealisticTreeEntryRemoval();
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var addonsRoot = SeedFolder(game, "Addons");
            var addonFolder = SeedFolder(game, addon.Name, addonsRoot);
            var scripts = SeedFolder(game, "Scripts", addonFolder);
            var resources = SeedFolder(game, "Resources", addonFolder);

            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(Db.TreeEntries.Any(x => x.Id == scripts.Id));
            Assert.False(Db.TreeEntries.Any(x => x.Id == resources.Id));
            Assert.False(Db.TreeEntries.Any(x => x.Id == addonFolder.Id));
            Assert.False(Db.TreeEntries.Any(x => x.Id == addonsRoot.Id));
        }

        [Fact]
        public async Task Handle_LeavesSharedAddonsRootInPlace_WhenAnotherAddonStillHasAFolderThere()
        {
            SetupRealisticTreeEntryRemoval();
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var addonsRoot = SeedFolder(game, "Addons");
            var addonFolder = SeedFolder(game, addon.Name, addonsRoot);
            // Another addon's folder under the same shared root — untouched by this uninstall.
            SeedFolder(game, "Pathfinder", addonsRoot);

            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(Db.TreeEntries.Any(x => x.Id == addonFolder.Id));
            Assert.True(Db.TreeEntries.Any(x => x.Id == addonsRoot.Id), "Shared 'Addons' root should survive while another addon still has a folder under it.");
        }

        [Fact]
        public async Task Handle_LeavesAddonFolderInPlace_WhenGmAddedExtraContentToIt()
        {
            SetupRealisticTreeEntryRemoval();
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var addonsRoot = SeedFolder(game, "Addons");
            var addonFolder = SeedFolder(game, addon.Name, addonsRoot);
            // A tree entry the addon didn't create (e.g. the GM dragged a file in here) — a real
            // file, not a folder, so it must be seeded with isFolder: false. Getting this wrong
            // previously made the test a false positive: TryRemoveEmptyAddonFoldersAsync's own
            // subfolder-sweep only ever touches IsFolder==true children, so a folder-flagged
            // "file" here got swept up and deleted as if it were an empty subfolder, leaving
            // addonFolder looking empty too.
            SeedFolder(game, "gm-added.png", addonFolder, isFolder: false);

            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.True(Db.TreeEntries.Any(x => x.Id == addonFolder.Id), "Addon folder with unrelated GM content should not be deleted.");
        }
    }
}

using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.Card.AddCard;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.IO.Compression;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    // Covers guard clauses, archive/info.json parsing, folder creation & dedup,
    // and the scripts/resources/actions/templates/views happy paths.
    // Dependency-resolution logic is covered separately in
    // InstallAddonDependencyCommandHandlerTests.cs.
    public class InstallAddonCommandHandlerTests : HandlerTestBase
    {
        protected readonly Mock<IMediator> Mediator = new();
        protected readonly Mock<IAddonRepositoryService> RepositoryService = new();
        protected readonly Mock<IGameEventLogger> Logger = new();

        public InstallAddonCommandHandlerTests()
        {
            Mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddTreeEntryCommand cmd, CancellationToken _) =>
                    (CommandResponse.Ok, new List<TreeEntryDto> { new TreeEntryDto { Id = Guid.NewGuid(), Name = cmd.TreeEntryDto.Name } }));

            Mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddResourceCommand cmd, CancellationToken _) =>
                {
                    var id = Guid.NewGuid();
                    Db.Resources.Add(new ResourceModel { Id = id, Name = cmd.Name, Key = cmd.Key, GameId = cmd.GameID!.Value, PlayerId = PlayerId });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, (Guid?)id);
                });

            Mediator.Setup(m => m.Send(It.IsAny<AddActionCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddActionCommand cmd, CancellationToken _) =>
                {
                    var game = Db.Games.First(g => g.Id == cmd.GameId);
                    var id = Guid.NewGuid();
                    Db.Actions.Add(new ActionModel { Id = id, Name = cmd.Action.Name, Content = cmd.Action.Content ?? "[]", Prefix = cmd.Action.Prefix ?? "addon", Game = game });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, id);
                });

            Mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddCardCommand cmd, CancellationToken _) =>
                {
                    var id = Guid.NewGuid();
                    Db.Cards.Add(new CardModel { Id = id, Name = cmd.Dto.Name, GameId = cmd.GameID, IsTemplate = cmd.IsTemplate, IsCustomUi = cmd.IsCustomUi, Properties = new List<PropertyModel>() });
                    Db.SaveChanges();
                    return (CommandResponse.Ok, id);
                });
        }

        private protected InstallAddonCommandHandler Handler() => new(Db, Mapper, RepositoryService.Object, Mediator.Object, Logger.Object);

        protected static byte[] BuildAddonZip(string infoJson, params (string Path, string Content)[] entries)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(archive, "info.json", infoJson);
                foreach (var (path, content) in entries)
                    WriteEntry(archive, path, content);
            }
            return ms.ToArray();
        }

        private static void WriteEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        protected const string BasicInfoJson = "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\"}";

        protected static InstallAddonCommand ValidCommand(GameModel game, PlayerDTO player, byte[] file) => new()
        {
            GameID = game.Id,
            Player = player,
            AddonFile = file,
        };

        // ---- Guard clauses ----

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new InstallAddonCommand { GameID = Guid.NewGuid(), Player = Player(), AddonFile = BuildAddonZip(BasicInfoJson) };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_PlayerLacksEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = ValidCommand(game, new PlayerDTO { Id = strangerId, Name = "Stranger" }, BuildAddonZip(BasicInfoJson));

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoAddonFileAndNoSourceKey_ThrowsArgumentException()
        {
            var game = BuildGame();
            var cmd = new InstallAddonCommand { GameID = game.Id, Player = Player(), AddonFile = null, AddonSourceKey = null };

            await Assert.ThrowsAsync<ArgumentException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AddonSourceKeyProvided_FetchesFileFromRepositoryService()
        {
            var game = BuildGame();
            RepositoryService.Setup(r => r.GetAddonByKey("dnd5e")).ReturnsAsync(BuildAddonZip(BasicInfoJson));
            var cmd = new InstallAddonCommand { GameID = game.Id, Player = Player(), AddonSourceKey = "dnd5e" };

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("dnd5e", result.AddonKey);
            RepositoryService.Verify(r => r.GetAddonByKey("dnd5e"), Times.Once);
        }

        // ---- Archive / info.json parsing ----

        [Fact]
        public async Task Handle_ArchiveMissingInfoJson_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                archive.CreateEntry("scripts/foo.js");
            var cmd = ValidCommand(game, Player(), ms.ToArray());

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InfoJsonDeserializesToNull_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            var cmd = ValidCommand(game, Player(), BuildAddonZip("null"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_InfoJsonMissingKeyField_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            var cmd = ValidCommand(game, Player(), BuildAddonZip("{\"name\":\"DnD 5e\",\"version\":\"1.0\"}"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        // ---- Folder creation & dedup ----

        [Fact]
        public async Task Handle_FoldersAlreadyExist_ReusesThemInsteadOfCreatingDuplicates()
        {
            var game = BuildGame();
            var addonsFolder = new TreeEntryModel { Id = Guid.NewGuid(), Name = "Addons", EntryType = nameof(ResourceModel), IsFolder = true, Game = game };
            Db.TreeEntries.Add(addonsFolder);
            Db.SaveChanges();
            var addonFolder = new TreeEntryModel { Id = Guid.NewGuid(), Name = "DnD 5e", EntryType = nameof(ResourceModel), IsFolder = true, Parent = addonsFolder, Game = game };
            Db.TreeEntries.Add(addonFolder);
            Db.SaveChanges();

            var cmd = ValidCommand(game, Player(), BuildAddonZip(BasicInfoJson));
            await Handler().Handle(cmd, CancellationToken.None);

            // "Addons" and "DnD 5e" already existed and must be reused; only the
            // always-created "Scripts"/"Resources" subfolders should be sent.
            Mediator.Verify(m => m.Send(It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.Name == "Addons"), It.IsAny<CancellationToken>()), Times.Never);
            Mediator.Verify(m => m.Send(It.Is<AddTreeEntryCommand>(c => c.TreeEntryDto.Name == "DnD 5e"), It.IsAny<CancellationToken>()), Times.Never);
            Mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        // ---- Happy path: scripts, resources, actions, templates, views ----

        [Fact]
        public async Task Handle_FullAddon_InstallsAllContentAndPersistsAddon()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson,
                ("scripts/main.js", "console.log('hi');"),
                ("resources/icon.png", "fake-binary"),
                ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"content\":\"[]\"}"),
                ("templates/monster.json", "{\"name\":\"Monster Template\",\"description\":\"d\"}"),
                ("views/panel.json", "{\"name\":\"Panel\",\"description\":\"d\"}"));
            var cmd = ValidCommand(game, Player(), archive);

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("dnd5e", result.AddonKey);
            Assert.Equal("DnD 5e", result.AddonName);
            Assert.Equal("1.0", result.AddonVersion);

            var addon = Db.Addons
                .Include(a => a.Resources).Include(a => a.Actions).Include(a => a.Templates).Include(a => a.Views)
                .First(a => a.Id == result.AddonId);
            Assert.Equal(2, addon.Resources!.Count); // script + resource
            Assert.Single(addon.Actions!);
            Assert.Single(addon.Templates!);
            Assert.Single(addon.Views!);

            Mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mediator.Verify(m => m.Send(It.IsAny<AddActionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.IsTemplate), It.IsAny<CancellationToken>()), Times.Once);
            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.IsCustomUi), It.IsAny<CancellationToken>()), Times.Once);
            // "Addons" + addon name + "Scripts" + "Resources" folders
            Mediator.Verify(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(4));
        }

        [Fact]
        public async Task Handle_ActionPrefixIsSetToAddonKey()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"content\":\"[]\"}"));
            var cmd = ValidCommand(game, Player(), archive);

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.Is<AddActionCommand>(c => c.Action.Prefix == "dnd5e"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ScriptAlreadyExists_ReinstallFalse_SkipsWithoutCallingAddResource()
        {
            var game = BuildGame();
            var existing = new ResourceModel { Id = Guid.NewGuid(), Name = "main.js", Key = "dnd5e_main.js", GameId = game.Id, PlayerId = PlayerId };
            Db.Resources.Add(existing);
            Db.SaveChanges();

            var archive = BuildAddonZip(BasicInfoJson, ("scripts/main.js", "console.log('hi');"));
            var cmd = ValidCommand(game, Player(), archive);
            cmd.Reinstall = false;

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(Db.Resources.Any(r => r.Id == existing.Id)); // untouched
        }

        [Fact]
        public async Task Handle_ScriptAlreadyExists_ReinstallTrue_RemovesOldAndInstallsNew()
        {
            var game = BuildGame();
            var existing = new ResourceModel { Id = Guid.NewGuid(), Name = "main.js", Key = "dnd5e_main.js", GameId = game.Id, PlayerId = PlayerId };
            Db.Resources.Add(existing);
            Db.SaveChanges();

            var archive = BuildAddonZip(BasicInfoJson, ("scripts/main.js", "console.log('hi');"));
            var cmd = ValidCommand(game, Player(), archive);
            cmd.Reinstall = true;

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.False(Db.Resources.Any(r => r.Id == existing.Id));
        }

        [Fact]
        public async Task Handle_AddResourceReturnsNullId_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            Mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.Ok, (Guid?)null));
            var archive = BuildAddonZip(BasicInfoJson, ("scripts/main.js", "console.log('hi');"));
            var cmd = ValidCommand(game, Player(), archive);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AddResourceIdNotPersisted_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            Mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.Ok, (Guid?)Guid.NewGuid())); // never actually persisted
            var archive = BuildAddonZip(BasicInfoJson, ("scripts/main.js", "console.log('hi');"));
            var cmd = ValidCommand(game, Player(), archive);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        // Regression: a leftover .gitkeep (or any dotfile — .DS_Store, editor
        // backups, etc.) inside resources/ used to reach AddResourceCommand,
        // whose extension isn't in ToMimeType()'s switch and used to crash the
        // whole install with a NullReferenceException (see
        // AddResourceCommandHandlerTests.Handle_UnrecognizedMimeType_...).
        // GetByFolder now filters these out entirely, for every folder
        // (resources/scripts/actions/templates/views), not just resources/.
        [Fact]
        public async Task Handle_DotfileInResourcesFolder_IsSkippedEntirely()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson,
                ("resources/.gitkeep", ""),
                ("resources/icon.png", "fake-binary"));
            var cmd = ValidCommand(game, Player(), archive);

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var addon = Db.Addons.Include(a => a.Resources).First(a => a.Id == result.AddonId);
            Assert.Single(addon.Resources!); // only icon.png, .gitkeep never reached AddResourceCommand
            Mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ViewWithUnknownCardId_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            Mediator.Setup(m => m.Send(It.IsAny<AddCardCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid())); // never actually added to Db.Cards
            var archive = BuildAddonZip(BasicInfoJson, ("views/panel.json", "{\"name\":\"Panel\",\"description\":\"d\"}"));
            var cmd = ValidCommand(game, Player(), archive);

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}

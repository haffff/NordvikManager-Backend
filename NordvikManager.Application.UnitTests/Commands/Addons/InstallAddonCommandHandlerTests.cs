using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Application.Commands.Card.AddCard;
using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.Resources;
using NordvikManager.Application.DataTransferObjects;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Interfaces;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using NordvikManager.Application.Services;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // Covers guard clauses, archive/info.json parsing, folder creation & dedup,
    // and the scripts/resources/actions/templates/views happy paths.
    // Dependency-resolution logic is covered separately in
    // InstallAddonDependencyCommandHandlerTests.cs.
    public class InstallAddonCommandHandlerTests : InstallAddonTestBase
    {
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
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
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

        // ---- Already-installed guard ----
        // Regression coverage: installing an addon that's already present for this game (e.g.
        // it arrived as another addon's dependency, then got selected directly too) used to
        // create a second AddonModel row and blindly re-run AddActions/AddTemplates/AddViews,
        // which have no existence check of their own — duplicating every action and card.

        [Fact]
        public async Task Handle_ActionWithGmPermission_DoesNotChangeTheOwnersRow()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"gmPermission\":0,\"content\":\"[]\"}"));

            await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            PermissionsMock.Verify(p => p.SetPermissions(game.MasterId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.None), Times.Never);
        }

        [Fact]
        public async Task Handle_AlreadyInstalled_WithoutReinstall_ReturnsAlreadyExistsAndDoesNotDuplicate()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"content\":\"[]\"}"));

            var (firstResponse, firstResult) = await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);
            var (secondResponse, secondResult) = await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, firstResponse);
            Assert.Equal(CommandResponse.AlreadyExists, secondResponse);
            Assert.Equal(firstResult.AddonId, secondResult.AddonId);

            Assert.Single(Db.Addons.Where(a => a.Key == "dnd5e"));
            Mediator.Verify(m => m.Send(It.IsAny<AddActionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_AlreadyInstalled_WithReinstall_UninstallsExistingFirstThenInstallsOnce()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"content\":\"[]\"}"));
            var (_, firstResult) = await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            // Mirrors UninstallAddonCommandHandler's real effect (remove the addon and its
            // actions) against the same InMemory Db the rest of this test class already shares —
            // Mediator is a full mock here, so this call needs its own real side effect wired up.
            Mediator.Setup(m => m.Send(It.Is<NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommand>(
                    c => c.AddonId == firstResult.AddonId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommand cmd, CancellationToken _) =>
                {
                    var addon = Db.Addons.Include(a => a.Actions).First(a => a.Id == cmd.AddonId);
                    Db.Actions.RemoveRange(addon.Actions);
                    Db.Addons.Remove(addon);
                    Db.SaveChanges();
                    return (CommandResponse.Ok, new NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommandResponse());
                });

            var reinstallCmd = ValidCommand(game, Player(), archive);
            reinstallCmd.Reinstall = true;
            var (response, result) = await Handler().Handle(reinstallCmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotEqual(firstResult.AddonId, result.AddonId);
            Assert.Single(Db.Addons.Where(a => a.Key == "dnd5e"));
            Assert.Single(Db.Actions.Where(a => a.Prefix == "dnd5e"));
        }

        // An update gives the addon's files new ids. Cards elsewhere in the game that point at
        // them (another addon's cards, cards made from its templates) used to keep the old,
        // deleted ids.
        [Fact]
        public async Task Handle_Reinstall_PointsTheGamesCardsAtTheNewFiles()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("resources/sheet.css", "body{}"), ("resources/logo.png", "png"));
            var (_, first) = await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);
            var oldSheet = Db.Resources.Single(r => r.Key == "dnd5e_sheet.css").Id;
            var oldLogo = Db.Resources.Single(r => r.Key == "dnd5e_logo.png").Id;
            var unrelated = Guid.NewGuid();
            var card = new CardModel
            {
                Id = Guid.NewGuid(), Name = "Other addon's sheet", GameId = game.Id, Properties = new List<PropertyModel>(),
                MainResource = oldSheet,
                AdditionalResources = System.Text.Json.JsonSerializer.Serialize(new[] { oldLogo, unrelated }),
            };
            Db.Cards.Add(card);
            Db.SaveChanges();
            // The real uninstall removes the addon and its files.
            Mediator.Setup(m => m.Send(It.Is<NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommand>(
                    c => c.AddonId == first.AddonId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommand cmd, CancellationToken _) =>
                {
                    var addon = Db.Addons.Include(a => a.Resources).First(a => a.Id == cmd.AddonId);
                    Db.Resources.RemoveRange(addon.Resources!);
                    Db.Addons.Remove(addon);
                    Db.SaveChanges();
                    return (CommandResponse.Ok, new NordvikManager.Application.Commands.Addons.UninstallAddon.UninstallAddonCommandResponse());
                });
            var reinstall = ValidCommand(game, Player(), archive);
            reinstall.Reinstall = true;

            await Handler().Handle(reinstall, CancellationToken.None);

            var newSheet = Db.Resources.Single(r => r.Key == "dnd5e_sheet.css").Id;
            var newLogo = Db.Resources.Single(r => r.Key == "dnd5e_logo.png").Id;
            Assert.NotEqual(oldSheet, newSheet);
            var stored = SeedContext().Cards.Single(c => c.Id == card.Id);
            Assert.Equal(newSheet, stored.MainResource);
            Assert.Equal(new[] { newLogo, unrelated }, System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(stored.AdditionalResources!));
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

        // An addon's action is added with the addon's key as its prefix, its hook filter as
        // written, and enabled.
        // Enabled is a regression: ActionDto.IsEnabled has no explicit default (unlike
        // AddonDto/AddonModel, which default to true), so it deserializes to false unless the
        // addon's own JSON says "isEnabled": true. Addon authors have no reason to write that
        // (an addon's actions are meant to work right after install), so without forcing it
        // every hook the addon declares (Install included) would silently never run.
        [Fact]
        public async Task Handle_Action_IsAddedPrefixedEnabledAndWithItsHookFilter()
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("actions/chat.json", "{\"name\":\"chat\",\"hook\":19,\"hookFilter\":\"poll, vote\",\"content\":\"[]\"}"));
            var cmd = ValidCommand(game, Player(), archive);

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.Is<AddActionCommand>(c => c.Action.Prefix == "dnd5e"), It.IsAny<CancellationToken>()), Times.Once);
            Mediator.Verify(m => m.Send(It.Is<AddActionCommand>(c => c.Action.IsEnabled), It.IsAny<CancellationToken>()), Times.Once);
            Mediator.Verify(m => m.Send(It.Is<AddActionCommand>(c => c.Action.HookFilter == "poll, vote"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ActionRefused_FailsTheInstall_NamingTheFile()
        {
            var game = BuildGame();
            Mediator.Setup(m => m.Send(It.IsAny<AddActionCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(WrongArgumentsException.Because("A Chat Command action needs the commands it handles in its hook filter, e.g. poll, vote."));
            var archive = BuildAddonZip(BasicInfoJson, ("actions/chat.json", "{\"name\":\"chat\",\"hook\":19,\"content\":\"[]\"}"));
            var cmd = ValidCommand(game, Player(), archive);

            var e = await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
            Assert.Contains("chat.json", e.Message);
            Assert.Contains("hook filter", e.Message);
        }

        // A script whose key is already taken: the GM's own copy stays unless reinstalling; a
        // player's upload squatting the key is replaced either way.
        [Theory]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        [InlineData(false, false, true)]
        public async Task Handle_ScriptKeyTaken(bool takenByTheGm, bool reinstall, bool replaced)
        {
            var game = BuildGame();
            var existing = new ResourceModel { Id = Guid.NewGuid(), Name = "main.js", Key = "dnd5e_main.js", GameId = game.Id, PlayerId = takenByTheGm ? PlayerId : PlayerWithoutEditOnTheGame() };
            Db.Resources.Add(existing);
            Db.SaveChanges();

            var cmd = ValidCommand(game, Player(), BuildAddonZip(BasicInfoJson, ("scripts/main.js", "console.log('hi');")));
            cmd.Reinstall = reinstall;

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), replaced ? Times.Once() : Times.Never());
            Assert.Equal(!replaced, Db.Resources.Any(r => r.Id == existing.Id));
        }

        // A player can upload a resource under any free key (Resources.Global.Create). If it
        // takes the key of an addon's file first, the install must not adopt it: the
        // player's file would become the addon's code, run by whoever opens its cards.
        private Guid PlayerWithoutEditOnTheGame()
        {
            var squatter = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(squatter, It.Is<NordvikManager.Domain.Entities.Interfaces.IEntity>(e => e is GameModel), It.IsAny<Permission>()))
                .Returns(false);
            return squatter;
        }

        [Fact]
        public async Task Handle_TemplateReferringToAPlayersUploadByKey_DoesNotUseIt()
        {
            var game = BuildGame();
            var squatted = Guid.NewGuid();
            Db.Resources.Add(new ResourceModel { Id = squatted, Name = "x.js", Key = "shared_x.js", GameId = game.Id, PlayerId = PlayerWithoutEditOnTheGame() });
            Db.SaveChanges();

            var archive = BuildAddonZip(BasicInfoJson,
                ("templates/monster.json", "{\"name\":\"Monster\",\"description\":\"d\",\"mainResource\":\"shared_x.js\",\"additionalResources\":[\"shared_x.js\"]}"));

            await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.IsTemplate
                && c.Dto.MainResource == null
                && !c.Dto.AdditionalResources!.Contains(squatted)), It.IsAny<CancellationToken>()), Times.Once);
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

        // The handler used to load every resource (file bytes included), tree entry, card,
        // action and property of the game up front. Installing into a game that already has
        // content must leave all of it unloaded, and key references must still resolve.
        [Fact]
        public async Task Handle_DoesNotLoadTheGamesExistingContent()
        {
            var game = BuildGame();
            var sharedMapId = Guid.NewGuid();
            var existing = new List<Guid> { sharedMapId };
            Db.Resources.Add(new ResourceModel { Id = sharedMapId, Name = "map.png", Key = "shared_map.png", GameId = game.Id, PlayerId = PlayerId, Data = new byte[4096] });
            for (int i = 0; i < 3; i++)
            {
                var resourceId = Guid.NewGuid();
                existing.Add(resourceId);
                Db.Resources.Add(new ResourceModel { Id = resourceId, Name = $"r{i}.png", Key = $"other_{i}", GameId = game.Id, PlayerId = PlayerId, Data = new byte[4096] });
                Db.Cards.Add(new CardModel { Id = Guid.NewGuid(), Name = $"Card {i}", GameId = game.Id, Properties = new List<PropertyModel> { new PropertyModel { Id = Guid.NewGuid(), Name = "hp", Value = "1" } } });
                Db.Actions.Add(new ActionModel { Id = Guid.NewGuid(), Name = $"Action {i}", Content = "[]", Prefix = "x", Game = game });
            }
            Db.SaveChanges();
            Db.ChangeTracker.Clear();

            var archive = BuildAddonZip(BasicInfoJson,
                ("resources/icon.png", "fake-binary"),
                ("actions/greet.json", "{\"name\":\"Greet\",\"description\":\"d\",\"content\":\"[]\"}"),
                ("templates/monster.json", "{\"name\":\"Monster\",\"description\":\"d\",\"mainResource\":\"dnd5e_icon.png\",\"additionalResources\":[\"shared_map.png\"]}"),
                ("views/panel.json", "{\"name\":\"Panel\",\"description\":\"d\"}"));

            var (response, result) = await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var iconId = Db.Resources.AsNoTracking().Single(r => r.Key == "dnd5e_icon.png").Id;
            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.IsTemplate
                && c.Dto.MainResource == iconId
                && c.Dto.AdditionalResources!.SequenceEqual(new[] { sharedMapId })), It.IsAny<CancellationToken>()), Times.Once);

            Assert.DoesNotContain(Db.ChangeTracker.Entries<ResourceModel>(), e => existing.Contains(e.Entity.Id));
            Assert.DoesNotContain(Db.ChangeTracker.Entries<CardModel>(), e => e.Entity.Name.StartsWith("Card "));
            Assert.DoesNotContain(Db.ChangeTracker.Entries<ActionModel>(), e => e.Entity.Name.StartsWith("Action "));
            Assert.Empty(Db.ChangeTracker.Entries<PropertyModel>());
        }

        // ---- The built-in Basics addon, as shipped ----

        // The source folder, found from this file's own path (the build output may be elsewhere).
        private static string BuiltInAddonsRoot([CallerFilePath] string thisFile = "") =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "NordvikManager", "BuiltInAddons"));

        [Fact]
        public async Task Handle_BuiltInBasicsAddon_InstallsTokenNoteTemplateAndActions()
        {
            var game = BuildGame();
            var basics = Assert.Single(new BuiltInAddons(BuiltInAddonsRoot()).GetAll(), a => a.FileName == "basics.zip");

            var (response, result) = await Handler().Handle(ValidCommand(game, Player(), basics.Data), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("basics", result.AddonKey);
            var addon = Db.Addons
                .Include(a => a.Resources).Include(a => a.Actions).Include(a => a.Templates)
                .Single(a => a.Key == "basics");
            Assert.Equal(
                new[] { "basics_note_index.html", "basics_quill.js", "basics_quill.snow.css", "basics_token_generic.json" },
                addon.Resources!.Select(r => r.Key).Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "add_token", "create_menus" }, addon.Actions!.Select(a => a.Name).Order(StringComparer.Ordinal));
            var note = Assert.Single(addon.Templates!);
            Assert.Equal("Note", note.Name);
            // the editor's script and stylesheet load with the note, and it opts into the app's styles
            var quillIds = addon.Resources!.Where(r => r.Key!.StartsWith("basics_quill")).Select(r => r.Id).ToList();
            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c =>
                c.IsTemplate
                && c.Dto.AdditionalResources != null && c.Dto.AdditionalResources.Count == 2 && quillIds.All(c.Dto.AdditionalResources.Contains)
                && c.Dto.Properties.Any(p => p.Name == "app_styles" && p.Value == "true")), It.IsAny<CancellationToken>()), Times.Once);
            // shared with every player (Read), so they can make their own notes from it
            PermissionsMock.Verify(p => p.SetGenericPermissions(It.Is<NordvikManager.Domain.Entities.Interfaces.IEntity>(e => e.Id == note.Id), Permission.Read), Times.Once);
        }

        // Addons can hide internal templates (e.g. a template only cloned by an importer)
        // from template lists with "hidden": true.
        [Theory]
        [InlineData("{\"name\":\"Internal\",\"description\":\"d\",\"hidden\":true}", true)]
        [InlineData("{\"name\":\"Monster\",\"description\":\"d\"}", false)]
        public async Task Handle_Template_HiddenFlagCarriedIntoTheCard(string templateJson, bool hidden)
        {
            var game = BuildGame();
            var archive = BuildAddonZip(BasicInfoJson, ("templates/t.json", templateJson));

            await Handler().Handle(ValidCommand(game, Player(), archive), CancellationToken.None);

            Mediator.Verify(m => m.Send(It.Is<AddCardCommand>(c => c.IsTemplate && (c.Dto.IsHidden ?? false) == hidden), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

using NordvikManager.Application.Commands.Addons.InstallAddon;
using NordvikManager.Application.Commands.Addons.UninstallAddon;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.IO.Compression;
using System.Text.Json;

namespace NordvikManager.Application.UnitTests.Commands.Addons
{
    // Covers dependency resolution. A nested InstallAddonCommand goes back through a real
    // handler, so chains, cycles and shared dependencies run as they do in a game; the
    // steps below an addon (files, cards, actions) stay mocked as in the base class.
    public class InstallAddonDependencyCommandHandlerTests : InstallAddonCommandHandlerTests
    {
        private const string InfoJsonWithDependency =
            "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\",\"dependencies\":[{\"name\":\"Core\",\"key\":\"core\",\"version\":\"2.0\"}]}";

        /// <summary>Keys of the addons a nested install was sent for, in order.</summary>
        private readonly List<string> nestedInstalls = new();

        public InstallAddonDependencyCommandHandlerTests()
        {
            Mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                .Returns((InstallAddonCommand c, CancellationToken t) =>
                {
                    nestedInstalls.Add(KeyOf(c.AddonFile!));
                    return Handler().Handle(c, t);
                });
        }

        private static string Info(string key, string version = "1.0", params (string Key, string? Version)[] dependencies)
            => JsonSerializer.Serialize(new
            {
                name = key.ToUpperInvariant(),
                key,
                version,
                dependencies = dependencies.Select(d => new { key = d.Key, version = d.Version }),
            });

        /// <summary>The registry answers with these addons, by key.</summary>
        private void Registry(params string[] infoJsons)
        {
            foreach (var info in infoJsons)
            {
                var file = BuildAddonZip(info);
                RepositoryService.Setup(r => r.GetAddonByKey(KeyOf(file))).ReturnsAsync(file);
            }
        }

        private static string KeyOf(byte[] zip)
        {
            using var archive = new ZipArchive(new MemoryStream(zip));
            using var stream = archive.GetEntry("info.json")!.Open();
            return JsonDocument.Parse(stream).RootElement.GetProperty("key").GetString()!;
        }

        private AddonModel SeedInstalledAddon(GameModel game, string key, string? version, params AddonDependency[] dependencies)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key, Version = version, Dependencies = dependencies.ToList() };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

        private List<AddonModel> InstalledAddons(GameModel game)
            => SeedContext().Games.Include(g => g.Addons).Single(g => g.Id == game.Id).Addons.ToList();

        [Fact]
        public async Task Handle_DependencyAlreadyInstalledWithMatchingVersion_SkipsNestedInstall()
        {
            var game = BuildGame();
            SeedInstalledAddon(game, "core", "2.0");
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DependencyRequiresNullVersion_MatchesAnyInstalledVersion()
        {
            var game = BuildGame();
            SeedInstalledAddon(game, "core", "9.9");
            var infoJson = "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\",\"dependencies\":[{\"name\":\"Core\",\"key\":\"core\"}]}";
            var cmd = ValidCommand(game, Player(), BuildAddonZip(infoJson));

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // The version is the lowest one that works, so a newer installed one is fine.
        [Fact]
        public async Task Handle_InstalledDependencyNewerThanRequired_SkipsNestedInstall()
        {
            var game = BuildGame();
            SeedInstalledAddon(game, "core", "2.1");
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_DependencyMissingAndAutoInstallDepsFalse_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));
            cmd.AutoInstallDeps = false;

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
            Assert.Empty(InstalledAddons(game));
        }

        [Fact]
        public async Task Handle_DependencyMissingAndAutoInstallDepsTrue_InstallsItFromTheRegistry()
        {
            var game = BuildGame();
            Registry(Info("core", "2.0"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));
            cmd.AutoInstallDeps = true;

            await Handler().Handle(cmd, CancellationToken.None);

            RepositoryService.Verify(r => r.GetAddonByKey("core"), Times.Once);
            Mediator.Verify(m => m.Send(
                It.Is<InstallAddonCommand>(c => c.AutoInstallDeps == true && c.GameID == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(new[] { "core", "dnd5e" }, InstalledAddons(game).Select(a => a.Key).OrderBy(k => k));
        }

        [Fact]
        public async Task Handle_DependencyFetchReturnsNull_ThrowsInvalidOperationException()
        {
            var game = BuildGame();
            RepositoryService.Setup(r => r.GetAddonByKey("core")).ReturnsAsync((byte[])null!);
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));
            cmd.AutoInstallDeps = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoDependencies_DoesNotCallRepositoryServiceForDeps()
        {
            var game = BuildGame();
            var cmd = ValidCommand(game, Player(), BuildAddonZip(BasicInfoJson));

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---- Cycles ----

        // Used to download a and b in turn until GitHub or memory gave out.
        [Fact]
        public async Task Handle_DependencyCycle_FailsBeforeInstallingAnything()
        {
            var game = BuildGame();
            Registry(Info("b", "1.0", ("a", null)));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("b", null))));

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));

            Assert.Contains("a → b → a", e.Message);
            RepositoryService.Verify(r => r.GetAddonByKey("a"), Times.Never);
            Assert.Empty(InstalledAddons(game));
            Assert.Empty(nestedInstalls);
        }

        [Fact]
        public async Task Handle_AddonDependsOnItself_Fails()
        {
            var game = BuildGame();
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("a", null))));

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));

            Assert.Contains("a → a", e.Message);
            RepositoryService.Verify(r => r.GetAddonByKey(It.IsAny<string>()), Times.Never);
            Assert.Empty(InstalledAddons(game));
        }

        // ---- Versions ----

        // Used to answer AlreadyExists for the old version and install the addon anyway.
        [Fact]
        public async Task Handle_InstalledDependencyTooOld_IsUpdated()
        {
            var game = BuildGame();
            SeedInstalledAddon(game, "core", "1.0");
            Registry(Info("core", "2.0"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));

            await Handler().Handle(cmd, CancellationToken.None);

            Mediator.Verify(m => m.Send(
                It.Is<InstallAddonCommand>(c => c.Reinstall && KeyOf(c.AddonFile!) == "core"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_RegistryHasOnlyAnOlderDependency_FailsBeforeInstallingAnything()
        {
            var game = BuildGame();
            Registry(Info("core", "1.5"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(cmd, CancellationToken.None));

            Assert.Contains("2.0", e.Message);
            Assert.Contains("1.5", e.Message);
            Assert.Empty(InstalledAddons(game));
            Assert.Empty(nestedInstalls);
        }

        // ---- Updates ----

        // Used to uninstall the old version first, so the addon was simply gone.
        [Fact]
        public async Task Handle_UpdateWhoseDependencyCannotBeFetched_KeepsTheInstalledAddon()
        {
            var game = BuildGame();
            var installed = SeedInstalledAddon(game, "dnd5e", "1.0");
            RepositoryService.Setup(r => r.GetAddonByKey("core")).ThrowsAsync(new Exception("Remote returned 503"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency.Replace("\"1.0\"", "\"1.1\"")));
            cmd.Reinstall = true;

            await Assert.ThrowsAsync<Exception>(() => Handler().Handle(cmd, CancellationToken.None));

            Mediator.Verify(m => m.Send(It.IsAny<UninstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(installed.Id, Assert.Single(InstalledAddons(game)).Id);
        }

        // ---- Order and sharing ----

        [Fact]
        public async Task Handle_DependencyChain_InstallsDeepestFirst()
        {
            var game = BuildGame();
            Registry(Info("b", "1.0", ("c", null)), Info("c"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("b", null))));

            await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(new[] { "c", "b" }, nestedInstalls);
            Assert.Equal(new[] { "a", "b", "c" }, InstalledAddons(game).Select(a => a.Key).OrderBy(k => k));
        }

        [Fact]
        public async Task Handle_SharedDependency_IsFetchedAndInstalledOnce()
        {
            var game = BuildGame();
            Registry(Info("b", "1.0", ("d", null)), Info("c", "1.0", ("d", null)), Info("d"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("b", null), ("c", null))));

            await Handler().Handle(cmd, CancellationToken.None);

            RepositoryService.Verify(r => r.GetAddonByKey("d"), Times.Once);
            Assert.Equal(1, nestedInstalls.Count(k => k == "d"));
            Assert.Equal("d", nestedInstalls[0]);
            Assert.Equal(new[] { "a", "b", "c", "d" }, InstalledAddons(game).Select(a => a.Key).OrderBy(k => k));
        }

        // The controller fires each one's Install hook, dependencies first.
        [Fact]
        public async Task Handle_ReturnsTheDependenciesItInstalled_DeepestFirst()
        {
            var game = BuildGame();
            Registry(Info("b", "1.0", ("c", null)), Info("c"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("b", null))));

            var (_, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(new[] { "c", "b" }, result.InstalledDependencies.Select(d => d.AddonKey));
            Assert.All(result.InstalledDependencies, d => Assert.NotNull(d.AddonId));
        }

        // ---- Built-in addons ----

        // Basics ships with the server and isn't in the registry.
        [Fact]
        public async Task Handle_DependencyOnABuiltInAddon_InstallsTheServersCopy()
        {
            var game = BuildGame();
            var basics = BuildAddonZip(Info("basics", "0.1.3"));
            BuiltIns.Setup(b => b.GetAll()).Returns(new[] { new BuiltInAddon("basics", "Basics", null, "0.1.3", "basics.zip", basics) });
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("basics", "0.1.0"))));

            await Handler().Handle(cmd, CancellationToken.None);

            RepositoryService.Verify(r => r.GetAddonByKey(It.IsAny<string>()), Times.Never);
            Assert.Equal(new[] { "a", "basics" }, InstalledAddons(game).Select(a => a.Key).OrderBy(k => k));
        }

        [Fact]
        public async Task Handle_BuiltInCopyTooOld_UsesTheRegistry()
        {
            var game = BuildGame();
            BuiltIns.Setup(b => b.GetAll()).Returns(new[] { new BuiltInAddon("basics", "Basics", null, "0.1.3", "basics.zip", BuildAddonZip(Info("basics", "0.1.3"))) });
            Registry(Info("basics", "0.2.0"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(Info("a", "1.0", ("basics", "0.2.0"))));

            await Handler().Handle(cmd, CancellationToken.None);

            RepositoryService.Verify(r => r.GetAddonByKey("basics"), Times.Once);
            Assert.Equal("0.2.0", InstalledAddons(game).Single(a => a.Key == "basics").Version);
        }

        // ---- Stored ----

        // Used to be thrown away at install, so nothing knew one addon needs another.
        [Fact]
        public async Task Handle_StoresTheAddonsDependencies()
        {
            var game = BuildGame();
            Registry(Info("core", "2.0"));
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));

            await Handler().Handle(cmd, CancellationToken.None);

            var stored = Assert.Single(InstalledAddons(game).Single(a => a.Key == "dnd5e").Dependencies!);
            Assert.Equal("core", stored.Key);
            Assert.Equal("2.0", stored.Version);
        }
    }
}

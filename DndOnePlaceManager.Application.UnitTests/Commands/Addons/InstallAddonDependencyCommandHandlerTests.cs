using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Domain.Entities;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    // Covers FindAndInstallDependencies: already-installed detection, the
    // AutoInstallDeps=false guard, and auto-install recursion via a nested
    // mediator.Send(InstallAddonCommand) (the nested send is mocked — it is not
    // actually re-entered, since IMediator itself is mocked here).
    public class InstallAddonDependencyCommandHandlerTests : InstallAddonCommandHandlerTests
    {
        private const string InfoJsonWithDependency =
            "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\",\"dependencies\":[{\"name\":\"Core\",\"key\":\"core\",\"version\":\"2.0\"}]}";

        private AddonModel SeedInstalledAddon(GameModel game, string key, string? version)
        {
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = key, Key = key, Version = version };
            Db.Addons.Add(addon);
            Db.Entry(addon).Property("GameModelId").CurrentValue = game.Id;
            Db.SaveChanges();
            return addon;
        }

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
            SeedInstalledAddon(game, "core", "9.9"); // different version, but requirement has no version constraint
            var infoJson = "{\"name\":\"DnD 5e\",\"key\":\"dnd5e\",\"version\":\"1.0\",\"dependencies\":[{\"name\":\"Core\",\"key\":\"core\"}]}";
            var cmd = ValidCommand(game, Player(), BuildAddonZip(infoJson));

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
        }

        [Fact]
        public async Task Handle_DependencyMissingAndAutoInstallDepsTrue_SendsNestedInstallAddonCommand()
        {
            var game = BuildGame();
            var depFile = BuildAddonZip("{\"name\":\"Core\",\"key\":\"core\",\"version\":\"2.0\"}");
            RepositoryService.Setup(r => r.GetAddonByKey("core")).ReturnsAsync(depFile);
            var cmd = ValidCommand(game, Player(), BuildAddonZip(InfoJsonWithDependency));
            cmd.AutoInstallDeps = true;

            await Handler().Handle(cmd, CancellationToken.None);

            RepositoryService.Verify(r => r.GetAddonByKey("core"), Times.Once);
            Mediator.Verify(m => m.Send(
                It.Is<InstallAddonCommand>(c => c.AddonFile == depFile && c.AutoInstallDeps == true && c.GameID == game.Id),
                It.IsAny<CancellationToken>()), Times.Once);
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
    }
}

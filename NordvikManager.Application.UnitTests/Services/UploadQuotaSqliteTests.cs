using Moq;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Application.UnitTests.Services
{
    // The upload quota's query on a real database (it runs on every player's resource write):
    // blob sizes are summed by SQL (length()), the blobs themselves are never loaded, and addon
    // files are left out by their addon link.
    public class UploadQuotaSqliteTests : SqliteHandlerTestBase
    {
        private const long MB = 1024 * 1024;
        private readonly Guid _gameId = Guid.NewGuid();
        private readonly ServerSettings _saved = new();

        public UploadQuotaSqliteTests()
        {
            using var seed = SeedContext();
            seed.Games.Add(new GameModel
            {
                Id = _gameId, Name = "G", SystemPlayerId = Guid.NewGuid(), MasterId = PlayerId,
                Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "GM" } },
            });
            var addon = new AddonModel { Id = Guid.NewGuid(), Name = "dnd5e", Key = "dnd5e" };
            seed.Addons.Add(addon);
            seed.Resources.Add(new ResourceModel { Id = Guid.NewGuid(), GameId = _gameId, PlayerId = PlayerId, Name = "map.png", Data = new byte[3 * MB] });
            var fromAddon = new ResourceModel { Id = Guid.NewGuid(), GameId = _gameId, PlayerId = PlayerId, Name = "dnd5e_srd.json", Data = new byte[5 * MB] };
            seed.Resources.Add(fromAddon);
            seed.Entry(fromAddon).Property("AddonModelId").CurrentValue = addon.Id;
            seed.SaveChanges();
        }

        private Task Room(long adding)
        {
            var settings = new Mock<IServerSettingsStore>();
            settings.Setup(s => s.Load()).Returns(_saved);
            var uploads = new Mock<IUploadStore>();
            uploads.Setup(u => u.Unfinished(It.IsAny<Guid>(), It.IsAny<TimeSpan>())).Returns(Array.Empty<UploadInfo>());
            return new UploadQuota(settings.Object, new Mock<IFileStorageProvider>().Object, uploads.Object).EnsureRoomAsync(Db, _gameId, PlayerId, adding);
        }

        [Fact]
        public async Task SumsBlobsInTheDatabase_WithoutLoadingThem_AndLeavesAddonFilesOut()
        {
            _saved.Uploads.GmGameLimitMb = 4;
            Commands.Reset();

            await Room(1 * MB); // 3 MB used (the addon's 5 MB don't count)
            await Assert.ThrowsAsync<UploadLimitException>(() => Room(1 * MB + 1));

            Assert.Contains(Commands.Texts, sql => sql.Contains("length(", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(Commands.Texts, sql => sql.Contains("\"r\".\"Data\"") && !sql.Contains("length("));
        }
    }
}

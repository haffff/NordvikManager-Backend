using Microsoft.EntityFrameworkCore;
using Moq;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.Resources;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Infrastructure.Services.Settings;

namespace NordvikManager.Application.UnitTests.Services
{
    // How much may be uploaded into a game, set by the server's local admin: one pool for what
    // the game's GMs (owner and co-GMs) uploaded, one per player for everyone else. 0 = no
    // limit. Linked files (the host's own disk) and addon files don't count; uploads still
    // arriving do.
    public class UploadQuotaTests : HandlerTestBase
    {
        private const long MB = 1024 * 1024;
        private readonly Mock<IServerSettingsStore> _settings = new();
        private readonly Mock<IUploadStore> _uploads = new();
        private readonly ServerSettings _saved = new();
        private readonly GameModel _game;
        private readonly Guid _coGm = Guid.NewGuid();
        private readonly Guid _ana = Guid.NewGuid();
        private readonly Guid _bo = Guid.NewGuid();

        public UploadQuotaTests()
        {
            _settings.Setup(s => s.Load()).Returns(() => _saved);
            _uploads.Setup(u => u.Unfinished(It.IsAny<Guid>(), It.IsAny<TimeSpan>())).Returns(Array.Empty<UploadInfo>());
            _game = BuildGame();
            _game.MasterId = PlayerId;
            Db.SaveChanges();
            // the co-GM may edit the game; Ana and Bo may not
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(_ana, It.IsAny<IEntity>(), It.IsAny<Permission>())).Returns(false);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(_bo, It.IsAny<IEntity>(), It.IsAny<Permission>())).Returns(false);
        }

        private UploadQuota Quota() => new(_settings.Object, Storage, _uploads.Object);

        private ResourceModel Seed(Guid owner, long size, ResourceStorageKind storage = ResourceStorageKind.Blob, Guid? addon = null)
        {
            var r = new ResourceModel { Id = Guid.NewGuid(), GameId = _game.Id, PlayerId = owner, Name = "f", Storage = storage };
            if (storage == ResourceStorageKind.Blob) r.Data = new byte[size];
            else
            {
                r.Path = "C:/files/" + r.Id;
                StorageMock.Setup(s => s.GetStamp(r.Path)).Returns(new FileStamp(DateTime.UtcNow, size));
            }
            Db.Resources.Add(r);
            if (addon != null)
            {
                Db.Addons.Add(new AddonModel { Id = addon.Value, Name = "a", Key = "a" });
                Db.Entry(r).Property("AddonModelId").CurrentValue = addon;
            }
            Db.SaveChanges();
            return r;
        }

        private Task Room(Guid player, long adding, Guid? replacing = null, Guid? exceptUpload = null) =>
            Quota().EnsureRoomAsync(Db, _game.Id, player, adding, replacing, exceptUpload);

        [Fact]
        public async Task APlayer_UpToTheLimit_ThenRefused_WithTheNumbersInTheMessage()
        {
            _saved.Uploads.PlayerLimitMb = 10;
            Seed(_ana, 6 * MB);
            Seed(_ana, 2 * MB, ResourceStorageKind.ManagedFile);
            Seed(_bo, 9 * MB); // someone else's

            await Room(_ana, 2 * MB);
            var refused = await Assert.ThrowsAsync<UploadLimitException>(() => Room(_ana, 2 * MB + 1));
            Assert.Contains("10 MB", refused.Message);
            Assert.Contains("8 MB", refused.Message);
        }

        [Fact]
        public async Task TheGms_ShareOnePool_AndHaveNoLimitByDefault()
        {
            Seed(PlayerId, 300 * MB);
            Seed(_coGm, 300 * MB);
            await Room(_coGm, 500 * MB); // 0: no limit

            _saved.Uploads.GmGameLimitMb = 700;
            await Room(PlayerId, 100 * MB);
            await Assert.ThrowsAsync<UploadLimitException>(() => Room(_coGm, 100 * MB + 1));
            // players' files are theirs, not the GMs'
            Seed(_ana, 50 * MB);
            await Room(PlayerId, 100 * MB);
        }

        [Fact]
        public async Task Players_Get200MbByDefault_AndZeroMeansNoLimit()
        {
            Assert.Equal(200, new ServerSettings().Uploads.PlayerLimitMb);
            Assert.Equal(0, new ServerSettings().Uploads.GmGameLimitMb);
            await Assert.ThrowsAsync<UploadLimitException>(() => Room(_ana, 201 * MB));

            _saved.Uploads.PlayerLimitMb = 0;
            await Room(_ana, 5000 * MB);
        }

        [Fact]
        public async Task LinkedFiles_AndAddonFiles_DontCount()
        {
            _saved.Uploads.GmGameLimitMb = 10;
            Seed(PlayerId, 500 * MB, ResourceStorageKind.Linked);
            Seed(PlayerId, 9 * MB, addon: Guid.NewGuid());

            await Room(PlayerId, 10 * MB);
        }

        [Fact]
        public async Task UploadsStillArriving_Count_ExceptTheOneBeingFinished()
        {
            _saved.Uploads.PlayerLimitMb = 10;
            var arriving = new UploadInfo { Id = Guid.NewGuid(), GameId = _game.Id, PlayerId = _ana, Size = 8 * MB };
            _uploads.Setup(u => u.Unfinished(_game.Id, It.IsAny<TimeSpan>())).Returns(new[] { arriving });

            await Assert.ThrowsAsync<UploadLimitException>(() => Room(_ana, 3 * MB));
            await Room(_ana, 8 * MB, exceptUpload: arriving.Id);
            await Room(_bo, 10 * MB);
        }

        [Fact]
        public async Task Replacing_ChecksOnlyTheGrowth()
        {
            _saved.Uploads.PlayerLimitMb = 10;
            var old = Seed(_ana, 9 * MB);

            await Room(_ana, 10 * MB, replacing: old.Id);
            await Assert.ThrowsAsync<UploadLimitException>(() => Room(_ana, 11 * MB, replacing: old.Id));

            // already over the limit (lowered since): replacing with a smaller file is always fine
            _saved.Uploads.PlayerLimitMb = 1;
            await Room(_ana, 5 * MB, replacing: old.Id);
        }
    }
}

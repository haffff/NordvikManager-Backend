using NordvikManager.Services.Implementations;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Services
{
    // Requests over WebRTC carry the user's id but no email (WebRTCInProcessAuthHandler), so a
    // server whose admins are listed by email must still know them inside a game.
    public class LocalAdminServiceTests : IDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"localadmin-{Guid.NewGuid():N}");

        public void Dispose()
        {
            if (File.Exists(_file)) File.Delete(_file);
        }

        private LocalAdminService Service(Dictionary<string, string?> settings) =>
            new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), _file);

        private static Dictionary<string, string?> ByEmail(string email) => new() { ["LocalAdmins:Emails:0"] = email };

        [Fact]
        public async Task ByEmail_TheSameUserWithoutEmail_IsStillAdmin()
        {
            var service = Service(ByEmail("gm@example.com"));

            Assert.True(await service.IsLocalAdminAsync("user-1", "gm@example.com"));
            Assert.True(await service.IsLocalAdminAsync("user-1", ""));
        }

        [Fact]
        public async Task ByEmail_RememberedAcrossRestarts()
        {
            Assert.True(await Service(ByEmail("gm@example.com")).IsLocalAdminAsync("user-1", "gm@example.com"));

            Assert.True(await Service(ByEmail("gm@example.com")).IsLocalAdminAsync("user-1", null));
        }

        [Fact]
        public async Task ByEmail_OtherUserWithoutEmail_IsNotAdmin()
        {
            var service = Service(ByEmail("gm@example.com"));
            await service.IsLocalAdminAsync("user-1", "gm@example.com");

            Assert.False(await service.IsLocalAdminAsync("user-2", ""));
        }

        [Fact]
        public async Task ByEmail_EmailRemovedFromSettings_NoLongerAdmin()
        {
            await Service(ByEmail("gm@example.com")).IsLocalAdminAsync("user-1", "gm@example.com");

            Assert.False(await Service(ByEmail("other@example.com")).IsLocalAdminAsync("user-1", ""));
        }

        [Fact]
        public async Task ByUserId_WorksWithoutEmail()
        {
            var service = Service(new() { ["LocalAdmins:UserIds:0"] = "user-1" });

            Assert.True(await service.IsLocalAdminAsync("user-1", ""));
            Assert.False(await service.IsLocalAdminAsync("user-2", ""));
        }

        [Fact]
        public async Task AutoPromote_FirstUserOnly_KeptAcrossRestarts()
        {
            Assert.True(await Service(new()).IsLocalAdminAsync("user-1", ""));

            var restarted = Service(new());
            Assert.True(await restarted.IsLocalAdminAsync("user-1", ""));
            Assert.False(await restarted.IsLocalAdminAsync("user-2", ""));
        }

        [Fact]
        public async Task AutoPromote_NobodyIsAdmin_WhenTheAdminFileCannotBeWritten()
        {
            // A file where its folder should be: every attempt to save fails.
            File.WriteAllText(_file, "");
            var service = new LocalAdminService(new ConfigurationBuilder().Build(), Path.Combine(_file, ".localadmin"));

            Assert.False(await service.IsLocalAdminAsync("user-1", ""));
            Assert.False(await service.IsLocalAdminAsync("user-2", ""));
            Assert.Null(service.GetAutoPromotedAdminId());
        }

        [Fact]
        public async Task AutoPromote_PromotesTheNextUser_OnceTheFileCanBeWrittenAgain()
        {
            File.WriteAllText(_file, "");
            var service = new LocalAdminService(new ConfigurationBuilder().Build(), Path.Combine(_file, ".localadmin"));
            await service.IsLocalAdminAsync("user-1", "");

            File.Delete(_file);

            Assert.True(await service.IsLocalAdminAsync("user-2", ""));
            Assert.False(await service.IsLocalAdminAsync("user-1", ""));
            Directory.Delete(_file, recursive: true);
        }

        [Theory]
        [InlineData("{ not json")]
        [InlineData("")]
        [InlineData("null")]
        public async Task AutoPromote_NobodyIsPromoted_WhenTheAdminFileIsDamaged(string content)
        {
            File.WriteAllText(_file, content);

            var service = Service(new());

            Assert.False(await service.IsLocalAdminAsync("user-1", ""));
            Assert.Equal(content, File.ReadAllText(_file));
        }

        [Fact]
        public async Task ConfiguredAdmins_StillAdmin_WhenTheAdminFileIsDamaged()
        {
            File.WriteAllText(_file, "{ not json");

            var service = Service(new() { ["LocalAdmins:UserIds:0"] = "user-1" });

            Assert.True(await service.IsLocalAdminAsync("user-1", ""));
            Assert.False(await service.IsLocalAdminAsync("user-2", ""));
        }

        [Fact]
        public async Task AutoPromote_CreatesTheFolder_AndLeavesNoTemporaryFile()
        {
            var folder = _file + "-dir";
            var path = Path.Combine(folder, "data", ".localadmin");
            try
            {
                var service = new LocalAdminService(new ConfigurationBuilder().Build(), path);

                Assert.True(await service.IsLocalAdminAsync("user-1", ""));

                Assert.Equal(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)!));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task LegacyFile_KeepsItsAdmin_WhenTheFileHasNotMovedYet()
        {
            var legacy = _file + "-legacy";
            try
            {
                var configuration = new ConfigurationBuilder().Build();
                await new LocalAdminService(configuration, legacy).IsLocalAdminAsync("user-1", "");

                var upgraded = new LocalAdminService(configuration, _file, legacy);

                Assert.False(await upgraded.IsLocalAdminAsync("user-2", ""));
                Assert.True(await upgraded.IsLocalAdminAsync("user-1", ""));
            }
            finally
            {
                if (File.Exists(legacy)) File.Delete(legacy);
            }
        }

        [Fact]
        public async Task LegacyFile_IsIgnored_WhenTheNewFileExists()
        {
            var legacy = _file + "-legacy";
            try
            {
                var configuration = new ConfigurationBuilder().Build();
                await new LocalAdminService(configuration, legacy).IsLocalAdminAsync("old-admin", "");
                await new LocalAdminService(configuration, _file).IsLocalAdminAsync("new-admin", "");

                var service = new LocalAdminService(configuration, _file, legacy);

                Assert.True(await service.IsLocalAdminAsync("new-admin", ""));
                Assert.False(await service.IsLocalAdminAsync("old-admin", ""));
            }
            finally
            {
                if (File.Exists(legacy)) File.Delete(legacy);
            }
        }

        [Fact]
        public async Task LegacyFile_DamagedBlocksPromotion_WhenTheFileHasNotMovedYet()
        {
            var legacy = _file + "-legacy";
            try
            {
                File.WriteAllText(legacy, "{ not json");

                var service = new LocalAdminService(new ConfigurationBuilder().Build(), _file, legacy);

                Assert.False(await service.IsLocalAdminAsync("user-1", ""));
            }
            finally
            {
                File.Delete(legacy);
            }
        }
    }
}

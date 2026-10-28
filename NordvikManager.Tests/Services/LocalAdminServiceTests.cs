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
    }
}

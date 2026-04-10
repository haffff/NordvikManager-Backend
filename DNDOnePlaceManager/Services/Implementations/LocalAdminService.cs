using DNDOnePlaceManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    public class LocalAdminService : ILocalAdminService
    {
        private readonly IConfiguration _configuration;
        private readonly string _adminFilePath;
        private string? _autoPromotedAdminId;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public LocalAdminService(IConfiguration configuration)
        {
            _configuration = configuration;
            _adminFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".localadmin");
            LoadAutoPromotedAdmin();
        }

        public async Task<bool> IsLocalAdminAsync(string userId, string? email)
        {
            var configuredAdmins = GetConfiguredAdmins();

            if (configuredAdmins.userIds.Any() || configuredAdmins.emails.Any())
            {
                var isConfiguredAdmin = configuredAdmins.userIds.Contains(userId) ||
                                       (email != null && configuredAdmins.emails.Contains(email, StringComparer.OrdinalIgnoreCase));
                return isConfiguredAdmin;
            }

            var autoPromoteEnabled = _configuration.GetValue("LocalAdmins:AutoPromoteFirstUser", true);
            if (!autoPromoteEnabled)
                return false;

            await _lock.WaitAsync();
            try
            {
                if (_autoPromotedAdminId != null)
                {
                    return _autoPromotedAdminId == userId;
                }

                await PromoteFirstUserAsync(userId);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public string? GetAutoPromotedAdminId()
        {
            return _autoPromotedAdminId;
        }

        private (string[] userIds, string[] emails) GetConfiguredAdmins()
        {
            var userIds = _configuration.GetSection("LocalAdmins:UserIds").Get<string[]>() ?? Array.Empty<string>();
            var emails = _configuration.GetSection("LocalAdmins:Emails").Get<string[]>() ?? Array.Empty<string>();
            return (userIds, emails);
        }

        private void LoadAutoPromotedAdmin()
        {
            try
            {
                if (File.Exists(_adminFilePath))
                {
                    var json = File.ReadAllText(_adminFilePath);
                    var data = JsonSerializer.Deserialize<AdminData>(json);
                    _autoPromotedAdminId = data?.UserId;
                }
            }
            catch
            {
                // If file is corrupted, ignore and allow re-promotion
            }
        }

        private async Task PromoteFirstUserAsync(string userId)
        {
            try
            {
                var data = new AdminData { UserId = userId, PromotedAt = DateTime.UtcNow };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_adminFilePath, json);
                _autoPromotedAdminId = userId;

                Console.WriteLine($"╔════════════════════════════════════════════════════════════════╗");
                Console.WriteLine($"║  LOCAL ADMIN AUTO-PROMOTED                                     ║");
                Console.WriteLine($"║  User ID: {userId,-49} ║");
                Console.WriteLine($"║                                                                ║");
                Console.WriteLine($"║  This user was automatically promoted to Local Admin because   ║");
                Console.WriteLine($"║  they were the first to log in and no admins are configured.   ║");
                Console.WriteLine($"║                                                                ║");
                Console.WriteLine($"║  To change this, edit appsettings.json and add:                ║");
                Console.WriteLine($"║    \"LocalAdmins\": {{                                             ║");
                Console.WriteLine($"║      \"UserIds\": [\"your-user-id\"],                               ║");
                Console.WriteLine($"║      \"Emails\": [\"admin@example.com\"]                            ║");
                Console.WriteLine($"║    }}                                                           ║");
                Console.WriteLine($"║                                                                ║");
                Console.WriteLine($"║  Or to disable auto-promotion:                                 ║");
                Console.WriteLine($"║    \"LocalAdmins\": {{ \"AutoPromoteFirstUser\": false }}           ║");
                Console.WriteLine($"╚════════════════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to persist local admin promotion for user '{userId}'. Auto-promotion was not applied: {ex.Message}");
            }
        }

        private class AdminData
        {
            public string UserId { get; set; } = string.Empty;
            public DateTime PromotedAt { get; set; }
        }
    }
}

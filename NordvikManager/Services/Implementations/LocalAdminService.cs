using NordvikManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations
{
    public class LocalAdminService : ILocalAdminService
    {
        private readonly IConfiguration _configuration;
        private readonly string _adminFilePath;
        private string? _autoPromotedAdminId;
        // User id → the configured email it logged in with. Requests over WebRTC carry no
        // email, so an admin listed by email is recognised in game by the id remembered here.
        private Dictionary<string, string> _emailAdmins = new();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public LocalAdminService(IConfiguration configuration)
            : this(configuration, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".localadmin"))
        {
        }

        public LocalAdminService(IConfiguration configuration, string adminFilePath)
        {
            _configuration = configuration;
            _adminFilePath = adminFilePath;
            LoadAutoPromotedAdmin();
        }

        public async Task<bool> IsLocalAdminAsync(string userId, string? email)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var configuredAdmins = GetConfiguredAdmins();

            if (configuredAdmins.userIds.Any() || configuredAdmins.emails.Any())
            {
                if (configuredAdmins.userIds.Contains(userId))
                    return true;

                if (!string.IsNullOrEmpty(email))
                {
                    if (!configuredAdmins.emails.Contains(email, StringComparer.OrdinalIgnoreCase))
                        return false;
                    await RememberEmailAdminAsync(userId, email);
                    return true;
                }

                // No email (a WebRTC request): the id must have logged in with a listed email before.
                return _emailAdmins.TryGetValue(userId, out var remembered)
                    && configuredAdmins.emails.Contains(remembered, StringComparer.OrdinalIgnoreCase);
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
                    _autoPromotedAdminId = string.IsNullOrEmpty(data?.UserId) ? null : data.UserId;
                    _emailAdmins = data?.EmailAdmins ?? new();
                }
            }
            catch
            {
                // If file is corrupted, ignore and allow re-promotion
            }
        }

        private async Task RememberEmailAdminAsync(string userId, string email)
        {
            await _lock.WaitAsync();
            try
            {
                if (_emailAdmins.TryGetValue(userId, out var known) && string.Equals(known, email, StringComparison.OrdinalIgnoreCase))
                    return;
                _emailAdmins[userId] = email;
                await SaveAsync(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to remember local admin '{userId}': {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }

        private Task SaveAsync(DateTime promotedAt)
        {
            var data = new AdminData { UserId = _autoPromotedAdminId ?? string.Empty, PromotedAt = promotedAt, EmailAdmins = _emailAdmins };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            return File.WriteAllTextAsync(_adminFilePath, json);
        }

        private async Task PromoteFirstUserAsync(string userId)
        {
            try
            {
                _autoPromotedAdminId = userId;
                await SaveAsync(DateTime.UtcNow);

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
                _autoPromotedAdminId = null;
                Console.Error.WriteLine($"Warning: Failed to persist local admin promotion for user '{userId}'. Auto-promotion was not applied: {ex.Message}");
            }
        }

        private class AdminData
        {
            public string UserId { get; set; } = string.Empty;
            public DateTime PromotedAt { get; set; }
            public Dictionary<string, string>? EmailAdmins { get; set; }
        }
    }
}

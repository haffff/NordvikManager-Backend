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
        private readonly string? _legacyFilePath;
        private string? _autoPromotedAdminId;
        // The file exists but can't be read, so who the admin is isn't known: nobody gets
        // promoted, or anyone could take over by being the next to log in.
        private bool _adminFileDamaged;
        private bool _saveFailureReported;
        // User id → the configured email it logged in with. Requests over WebRTC carry no
        // email, so an admin listed by email is recognised in game by the id remembered here.
        private Dictionary<string, string> _emailAdmins = new();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public LocalAdminService(IConfiguration configuration)
            : this(
                configuration,
                configuration["LocalAdmins:FilePath"] ?? "./data/.localadmin",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".localadmin"))
        {
        }

        /// <param name="legacyFilePath">Where the file used to be: read until the first save writes the new one.</param>
        public LocalAdminService(IConfiguration configuration, string adminFilePath, string? legacyFilePath = null)
        {
            _configuration = configuration;
            _adminFilePath = Path.GetFullPath(adminFilePath);
            _legacyFilePath = legacyFilePath;
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
            if (!autoPromoteEnabled || _adminFileDamaged)
                return false;

            await _lock.WaitAsync();
            try
            {
                if (_autoPromotedAdminId != null)
                {
                    return _autoPromotedAdminId == userId;
                }

                return await PromoteFirstUserAsync(userId);
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
            var path = File.Exists(_adminFilePath) ? _adminFilePath : _legacyFilePath;
            if (path == null || !File.Exists(path))
                return;

            try
            {
                var data = JsonSerializer.Deserialize<AdminData>(File.ReadAllText(path))
                    ?? throw new JsonException("The file is empty.");
                _autoPromotedAdminId = string.IsNullOrEmpty(data.UserId) ? null : data.UserId;
                _emailAdmins = data.EmailAdmins ?? new();
            }
            catch (Exception ex)
            {
                _adminFileDamaged = true;
                Console.Error.WriteLine($"Warning: The local admin file '{path}' can't be read, so nobody will be auto-promoted to local admin. Fix or delete it and restart, or list admins under LocalAdmins in appsettings.json: {ex.Message}");
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
                if (!_adminFileDamaged) // Saving would replace it, and lose who it names.
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

        // Through a temporary file: a crash mid-write must not leave a damaged admin file.
        private async Task SaveAsync(DateTime promotedAt)
        {
            var data = new AdminData { UserId = _autoPromotedAdminId ?? string.Empty, PromotedAt = promotedAt, EmailAdmins = _emailAdmins };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(Path.GetDirectoryName(_adminFilePath)!);
            var temporary = _adminFilePath + ".tmp";
            await File.WriteAllTextAsync(temporary, json);
            File.Move(temporary, _adminFilePath, overwrite: true);
        }

        private async Task<bool> PromoteFirstUserAsync(string userId)
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
                return true;
            }
            catch (Exception ex)
            {
                // Not admin: an unsaved promotion would hand admin to whoever asks next, too.
                // The next request tries again, so a passing problem sorts itself out.
                _autoPromotedAdminId = null;
                if (!_saveFailureReported)
                {
                    _saveFailureReported = true;
                    Console.Error.WriteLine($"Warning: Failed to save the local admin file '{_adminFilePath}', so nobody is local admin yet: {ex.Message}");
                }
                return false;
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

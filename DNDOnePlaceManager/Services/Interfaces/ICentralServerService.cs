using DndOnePlaceManager.Application.DataTransferObjects.Game;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Interfaces
{
    /// <summary>
    /// Public metadata returned by the Central Server's /meta endpoint.
    /// Used to configure ICE servers and registration requirements without auth.
    /// </summary>
    public class CentralServerMeta
    {
        public bool IsInvitationRequired { get; set; }
        public List<string> StunServers { get; set; } = new();
        public string? TurnServer { get; set; }
        public string MinBackendVersion { get; set; } = "1.0.0";
    }

    /// <summary>
    /// Carries the Central Server session plus the decoded user identity.
    /// The CentralToken is only used server-side for proxied calls;
    /// it is never the GM Backend's auth token.
    /// </summary>
    public class CentralLoginResult
    {
        /// <summary>Central Server JWT — used only for server-to-server proxied calls.</summary>
        public string CentralToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }

        // User identity decoded from the Central Server JWT (no signature validation needed here —
        // we're just reading claims from a token the Central Server just issued to us).
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public bool IsAdmin { get; set; }
    }

    public interface ICentralServerService
    {
        Task<CentralServerMeta?> GetMetaAsync();
        Task<string?> CreateSessionAsync(string centralToken, GameItemDTO session);
        Task<CentralLoginResult?> LoginAsync(string username, string password);
        Task<(bool success, string message)> RegisterAsync(string username, string? email, string password, string inviteCode);
        Task<bool> CheckRegistrationKeyAsync(string key);
        Task<string?> GetUserNameAsync(string centralToken, string userId);
        Task<object?> GetInvitesAsync(string centralToken, int page);
        Task<string?> GenerateInviteAsync(string centralToken, int hours);
        Task<bool> DeleteInviteAsync(string centralToken, string key);
        Task<Dictionary<string, string>?> GetKeyboardBindingsAsync(string centralToken);
        Task<bool> SetKeyboardBindingsAsync(string centralToken, Dictionary<string, string> bindings);
    }
}

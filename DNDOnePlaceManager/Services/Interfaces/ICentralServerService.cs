using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public string MinBackendVersion { get; set; } = "1.0.0";
    }

    /// <summary>
    /// ICE configuration from the Central Server's authenticated /api/ice-servers endpoint:
    /// STUN plus, when configured, TURN with short-lived coturn credentials.
    /// </summary>
    public class IceServersResult
    {
        public List<IceServerDto> IceServers { get; set; } = new();
        /// <summary>Lifetime of the TURN credentials in seconds; null when TURN is not configured.</summary>
        public int? Ttl { get; set; }
    }

    /// <summary>One RTCIceServer entry; <c>urls</c> may be a single string or an array on the wire.</summary>
    public class IceServerDto
    {
        [JsonConverter(typeof(SingleOrArrayConverter))]
        public List<string> Urls { get; set; } = new();
        public string? Username { get; set; }
        public string? Credential { get; set; }
    }

    internal class SingleOrArrayConverter : JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            return token.Type switch
            {
                JTokenType.Array => token.ToObject<List<string>>() ?? new(),
                JTokenType.String => new List<string> { token.Value<string>()! },
                _ => new List<string>(),
            };
        }

        public override void WriteJson(JsonWriter writer, List<string>? value, JsonSerializer serializer)
            => serializer.Serialize(writer, value);
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
        /// <summary>Fetches ICE servers (incl. TURN credentials) for the given Central user; null on failure.</summary>
        Task<IceServersResult?> GetIceServersAsync(string centralToken);
        Task<string?> CreateSessionAsync(string centralToken, GameItemDTO session);
        Task<CentralLoginResult?> LoginAsync(string username, string password);
        /// <summary>
        /// Exchanges a Central Server refresh token for a new access token.
        /// Returns the new access token, or null if the refresh token is invalid/expired.
        /// </summary>
        Task<string?> RefreshTokenAsync(string refreshToken);
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

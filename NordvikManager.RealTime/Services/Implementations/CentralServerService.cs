using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations
{
    public class CentralServerService : ICentralServerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICentralTokenStore _tokens;
        private readonly string _centralServerUrl;

        public CentralServerService(
            IHttpClientFactory httpClientFactory,
            ICentralTokenStore tokens,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _tokens = tokens;
            _centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        }

        /// <summary>
        /// Sends an authenticated request against the Central Server. The CentralToken JWT is
        /// short-lived (15 min) and nothing proactively refreshes it, so long-running GM sessions
        /// eventually start getting 401s on every Central-proxied call. On a 401 here, this
        /// transparently refreshes with the host's refresh token (<see cref="ICentralTokenStore"/>:
        /// the native server's CentralRefreshToken cookie, same flow as the RefreshCentralToken
        /// endpoint), hands the new tokens back to the host for subsequent requests, and retries once.
        /// </summary>
        private async Task<HttpResponseMessage> SendAuthenticatedAsync(
            string accessToken,
            Func<HttpClient, Task<HttpResponseMessage>> send)
        {
            var response = await send(CreateAuthenticatedClient(accessToken));
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            // A caller with no access token (a tunnelled request carries no cookies) has no session
            // of its own to refresh: the store's token is someone else's (in the browser host, the
            // GM's login), and the retry would act as them.
            if (string.IsNullOrEmpty(accessToken))
                return response;

            var refreshToken = _tokens.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
                return response;

            var tokens = await RefreshTokenAsync(refreshToken);
            if (tokens == null)
                return response;

            _tokens.Refreshed(tokens);

            return await send(CreateAuthenticatedClient(tokens.AccessToken));
        }

        public async Task<CentralServerMeta?> GetMetaAsync()
        {
            var client = _httpClientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/meta");
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<CentralServerMeta>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetAddonRegistryAsync()
        {
            var client = _httpClientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/addons");
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<NordvikManager.Infrastructure.Interfaces.AddonReadmeResult?> GetAddonReadmeAsync(string key)
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{_centralServerUrl}/api/addons/{Uri.EscapeDataString(key)}/readme");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(CentralError(body) ?? $"The readme of '{key}' answered {(int)response.StatusCode}.");
            var json = JObject.Parse(body);
            return new NordvikManager.Infrastructure.Interfaces.AddonReadmeResult(json["url"]?.ToString() ?? "", json["markdown"]?.ToString() ?? "");
        }

        public async Task<byte[]> GetAddonPackageAsync(string centralToken, string key)
        {
            var response = await SendAuthenticatedAsync(centralToken,
                client => client.GetAsync($"{_centralServerUrl}/api/addons/{Uri.EscapeDataString(key)}/package"));
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsByteArrayAsync();
            var error = CentralError(await response.Content.ReadAsStringAsync());
            throw new InvalidOperationException($"Central couldn't download the addon '{key}': {error ?? ((int)response.StatusCode).ToString()}");
        }

        // Central's { "error": "..." }, if that's what it answered.
        private static string? CentralError(string body)
        {
            try { return JObject.Parse(body)["error"]?.ToString(); }
            catch { return null; }
        }

        public async Task<IceServersResult?> GetIceServersAsync(string centralToken)
        {
            try
            {
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.GetAsync($"{_centralServerUrl}/api/ice-servers"));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<IceServersResult>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> CreateSessionAsync(string centralToken, GameItemDTO session)
        {
            var body = JsonConvert.SerializeObject(new
            {
                name = session.Name,
                summary = session.ShortDescription,
                description = session.LongDescription,
                image = session.Image,
                isPublic = session.IsPublic,
                passwordRequired = session.PasswordRequired,
                password = session.Password
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.PostAsync($"{_centralServerUrl}/api/gamelist/addgame", content));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);
                return result["id"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<CentralSessionUpdate> UpdateSessionAsync(string centralToken, string sessionId, GameItemDTO session)
        {
            var fields = new JObject
            {
                ["sessionId"] = sessionId,
                ["name"] = session.Name,
                ["summary"] = session.ShortDescription,
                ["description"] = session.LongDescription,
                ["image"] = session.Image,
                ["isPublic"] = session.IsPublic,
            };
            // Heals a session made without the password (Central then can't check it on join).
            // Left out, Central keeps what it has.
            if (!string.IsNullOrEmpty(session.Password))
                fields["password"] = session.Password;
            var body = fields.ToString(Formatting.None);
            try
            {
                // Content built per attempt: a retry after a token refresh can't reuse a sent one.
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.PostAsync($"{_centralServerUrl}/api/gamelist/updategame",
                        new StringContent(body, Encoding.UTF8, "application/json")));
                if (response.IsSuccessStatusCode) return CentralSessionUpdate.Updated;
                return await IsSessionNotFoundAsync(response) ? CentralSessionUpdate.Missing : CentralSessionUpdate.Failed;
            }
            catch
            {
                return CentralSessionUpdate.Failed;
            }
        }

        // Central's own "no such session" answer. A Central too old for the route answers 404
        // too, but with Express's HTML page; that one must not count as a missing session.
        private static async Task<bool> IsSessionNotFoundAsync(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.NotFound || response.Content == null) return false;
            try
            {
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                return json["error"]?.Value<string>() == "Session not found";
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public async Task<bool> DeleteSessionAsync(string centralToken, string sessionId)
        {
            try
            {
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.DeleteAsync($"{_centralServerUrl}/api/gamelist/DeleteGame?gameId={Uri.EscapeDataString(sessionId)}"));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<CentralJoinResult> JoinSessionAsync(string centralToken, string sessionId, string? password)
        {
            var body = JsonConvert.SerializeObject(new { gameID = sessionId, password });
            try
            {
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.PostAsync($"{_centralServerUrl}/api/gamelist/join",
                        new StringContent(body, Encoding.UTF8, "application/json")));
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                    return CentralJoinResult.Joined;
                var status = (int)response.StatusCode;
                return status >= 400 && status < 500 ? CentralJoinResult.Refused : CentralJoinResult.Failed;
            }
            catch
            {
                return CentralJoinResult.Failed;
            }
        }

        public async Task<string?> GetSignalingTicketAsync(string centralToken, string sessionId)
        {
            var body = JsonConvert.SerializeObject(new { sessionId });
            try
            {
                var response = await SendAuthenticatedAsync(centralToken,
                    client => client.PostAsync($"{_centralServerUrl}/api/gamelist/signalingticket",
                        new StringContent(body, Encoding.UTF8, "application/json")));
                if (!response.IsSuccessStatusCode) return null;
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                return json["ticket"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        // refresh token → its refresh (in flight or done), kept briefly: Central revokes a refresh
        // token when it's used, so a second refresh with it (another request, another tab) must
        // get the first one's result rather than a refusal.
        private readonly ConcurrentDictionary<string, (DateTime At, Task<CentralTokens?> Refresh)> _refreshes = new();
        private static readonly TimeSpan RefreshReuse = TimeSpan.FromMinutes(1);

        public Task<CentralTokens?> RefreshTokenAsync(string refreshToken)
        {
            var now = DateTime.UtcNow;
            foreach (var kv in _refreshes)
                if (now - kv.Value.At > RefreshReuse) _refreshes.TryRemove(kv);

            return _refreshes.GetOrAdd(refreshToken, token => (now, RefreshOnceAsync(token))).Refresh;
        }

        // A failed refresh isn't reused: the next caller asks Central again.
        private async Task<CentralTokens?> RefreshOnceAsync(string refreshToken)
        {
            await Task.Yield(); // stored in _refreshes before it can finish
            var tokens = await RequestRefreshAsync(refreshToken);
            if (tokens == null)
                _refreshes.TryRemove(refreshToken, out _);
            return tokens;
        }

        private async Task<CentralTokens?> RequestRefreshAsync(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var body = JsonConvert.SerializeObject(new { refreshToken });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync($"{_centralServerUrl}/api/user/refresh", content);
                if (!response.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                var accessToken = json["accessToken"]?.Value<string>() ?? ExtractCookieValue(response, "Authorization");
                if (string.IsNullOrEmpty(accessToken)) return null;
                return new CentralTokens(accessToken, json["refreshToken"]?.Value<string>());
            }
            catch
            {
                return null;
            }
        }

        public async Task<CentralLoginResult?> LoginAsync(string username, string password)
        {
            var client = _httpClientFactory.CreateClient();
            var body = JsonConvert.SerializeObject(new { UserName = username, password });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync($"{_centralServerUrl}/api/user/login", content);
            }
            catch (Exception e)
            {
                // Log the exception (in real code, use a logging framework)
                Console.WriteLine($"Error connecting to Central Server: {e.Message}");

                return null;
            }

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var body2 = JObject.Parse(json);

            // The body has it (a browser-wasm host can't read Set-Cookie); the cookie is the
            // fallback for a Central that only set it there.
            string? centralToken = body2["accessToken"]?.Value<string>() ?? ExtractCookieValue(response, "Authorization");
            if (string.IsNullOrEmpty(centralToken)) return null;

            // Decode (not validate) the Central Server JWT to extract user identity.
            // We trust the claims because we just received this token directly from the Central Server
            // over a server-to-server call — no need to verify the signature here.
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.CanReadToken(centralToken) ? handler.ReadJwtToken(centralToken) : null;

            return new CentralLoginResult
            {
                CentralToken = centralToken,
                RefreshToken = body2["refreshToken"]?.Value<string>(),
                UserId = jwtToken?.Subject ?? string.Empty,
                UserName = jwtToken?.Claims.FirstOrDefault(c => c.Type == "username")?.Value,
                Email = jwtToken?.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
                IsAdmin = jwtToken?.Claims.FirstOrDefault(c => c.Type == "isAdmin")?.Value?.ToLowerInvariant() == "true"
            };
        }

        public async Task<(bool success, string message)> RegisterAsync(string username, string? email, string password, string inviteCode)
        {
            var client = _httpClientFactory.CreateClient();
            var body = JsonConvert.SerializeObject(new
            {
                UserName = username,
                email,
                password,
                confirmPassword = password,
                inviteCode
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync($"{_centralServerUrl}/api/user/register", content);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

            var json = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
                return (true, "User created successfully!");

            try
            {
                var error = JObject.Parse(json);
                return (false, error["error"]?.Value<string>() ?? "Registration failed");
            }
            catch
            {
                return (false, "Registration failed");
            }
        }

        public async Task<bool> CheckRegistrationKeyAsync(string key)
        {
            var client = _httpClientFactory.CreateClient();
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/user/CheckRegistrationKey?key={Uri.EscapeDataString(key)}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> GetUserNameAsync(string accessToken, string userId)
        {
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.GetAsync($"{_centralServerUrl}/api/user/GetUserNameById?id={Uri.EscapeDataString(userId)}"));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);
                return result["userName"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<object?> GetInvitesAsync(string accessToken, int page)
        {
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.GetAsync($"{_centralServerUrl}/api/user/invites?page={page}"));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GenerateInviteAsync(string accessToken, int hours)
        {
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.GetAsync($"{_centralServerUrl}/api/user/GenerateInvite?hours={hours}"));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JObject.Parse(json);
                return result["key"]?.Value<string>();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteInviteAsync(string accessToken, string key)
        {
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.DeleteAsync($"{_centralServerUrl}/api/user/deleteinvite?key={Uri.EscapeDataString(key)}"));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, string>?> GetKeyboardBindingsAsync(string accessToken)
        {
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.GetAsync($"{_centralServerUrl}/api/user/KeyboardBindings"));
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> SetKeyboardBindingsAsync(string accessToken, Dictionary<string, string> bindings)
        {
            var body = JsonConvert.SerializeObject(bindings);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var response = await SendAuthenticatedAsync(accessToken,
                    client => client.PostAsync($"{_centralServerUrl}/api/user/KeyboardBindings", content));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private HttpClient CreateAuthenticatedClient(string accessToken)
        {
            // Bearer rather than a Cookie header, which a browser-wasm host can't set; Central
            // takes either (Bearer since 2026-09-06).
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            return client;
        }

        private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
                return null;

            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase))
                    return cookie.Split(';')[0].Substring(cookieName.Length + 1);
            }

            return null;
        }
    }
}

using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    public class CentralServerService : ICentralServerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _centralServerUrl;

        public CentralServerService(
            IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
            _centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
        }

        /// <summary>
        /// Sends an authenticated request against the Central Server. The CentralToken JWT is
        /// short-lived (15 min) and nothing proactively refreshes it, so long-running GM sessions
        /// eventually start getting 401s on every Central-proxied call. On a 401 here, this
        /// transparently refreshes via the CentralRefreshToken cookie (same flow as the
        /// RefreshCentralToken endpoint), persists the new CentralToken cookie for subsequent
        /// requests, and retries once.
        /// </summary>
        private async Task<HttpResponseMessage> SendAuthenticatedAsync(
            string accessToken,
            Func<HttpClient, Task<HttpResponseMessage>> send)
        {
            var response = await send(CreateAuthenticatedClient(accessToken));
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            var httpContext = _httpContextAccessor.HttpContext;
            var refreshToken = httpContext?.Request.Cookies["CentralRefreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return response;

            var newAccessToken = await RefreshTokenAsync(refreshToken);
            if (newAccessToken == null)
                return response;

            if (httpContext != null)
            {
                httpContext.Response.Cookies.Append("CentralToken", newAccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.None,
                    Secure = true
                });
            }

            return await send(CreateAuthenticatedClient(newAccessToken));
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

        public async Task<string?> RefreshTokenAsync(string refreshToken)
        {
            var client = _httpClientFactory.CreateClient();
            var body = JsonConvert.SerializeObject(new { refreshToken });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync($"{_centralServerUrl}/api/user/refresh", content);
                if (!response.IsSuccessStatusCode) return null;

                return ExtractCookieValue(response, "Authorization");
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

            string? centralToken = ExtractCookieValue(response, "Authorization");
            if (centralToken == null) return null;

            var json = await response.Content.ReadAsStringAsync();
            var body2 = JObject.Parse(json);

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
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", $"Authorization={accessToken}");
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

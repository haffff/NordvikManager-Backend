using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    public class CentralServerService : ICentralServerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _centralServerUrl;

        public CentralServerService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
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
            var client = CreateAuthenticatedClient(centralToken);
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
                var response = await client.PostAsync($"{_centralServerUrl}/api/gamelist/addgame", content);
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
            var client = CreateAuthenticatedClient(accessToken);
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/user/GetUserNameById?id={Uri.EscapeDataString(userId)}");
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
            var client = CreateAuthenticatedClient(accessToken);
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/user/invites?page={page}");
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
            var client = CreateAuthenticatedClient(accessToken);
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/user/GenerateInvite?hours={hours}");
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
            var client = CreateAuthenticatedClient(accessToken);
            try
            {
                var response = await client.DeleteAsync($"{_centralServerUrl}/api/user/deleteinvite?key={Uri.EscapeDataString(key)}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, string>?> GetKeyboardBindingsAsync(string accessToken)
        {
            var client = CreateAuthenticatedClient(accessToken);
            try
            {
                var response = await client.GetAsync($"{_centralServerUrl}/api/user/KeyboardBindings");
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
            var client = CreateAuthenticatedClient(accessToken);
            var body = JsonConvert.SerializeObject(bindings);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync($"{_centralServerUrl}/api/user/KeyboardBindings", content);
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

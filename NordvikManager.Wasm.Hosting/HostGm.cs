using Newtonsoft.Json.Linq;
using NordvikManager.Api;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Interfaces;

namespace NordvikManager.Wasm.Hosting
{
    /// <summary>
    /// The browser host's own GM, using the host in its own tab: logs in to Central (the tokens
    /// stay in the host, <see cref="HeldCentralTokens"/>; a browser can't keep Central's cookies
    /// for it), then calls the same endpoints as everyone (game list, create a game, start its
    /// session) through <see cref="ApiRouter.InvokeAsync"/>, with those tokens as its cookies.
    /// The GM who logged in is the host's local admin (<see cref="HostLocalAdmin"/>).
    /// </summary>
    public sealed class HostGm
    {
        private const string AccessCookie = "CentralToken";
        private const string RefreshCookie = "CentralRefreshToken";

        private readonly ICentralServerService _central;
        private readonly HeldCentralTokens _tokens;
        private readonly ApiRouter _router;

        public HostGm(ICentralServerService central, HeldCentralTokens tokens, ApiRouter router)
        {
            _central = central;
            _tokens = tokens;
            _router = router;
        }

        /// <summary>The GM logged in here, or null.</summary>
        public User? User { get; private set; }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var login = await _central.LoginAsync(username, password);
            if (login == null || string.IsNullOrEmpty(login.CentralToken))
                return false;

            _tokens.Set(new CentralTokens(login.CentralToken, login.RefreshToken));
            User = new User
            {
                Id = login.UserId,
                UserName = login.UserName,
                Email = login.Email,
                IsAdmin = login.IsAdmin,
                IsLocalAdmin = true,
            };
            return true;
        }

        public void Logout()
        {
            User = null;
            _tokens.Clear();
        }

        /// <summary>
        /// An API call as the logged-in GM (e.g. POST api/GameList/AddGame): 401 before login.
        /// Central tokens an endpoint refreshed are kept for the next call.
        /// </summary>
        public async Task<(int Status, JToken? Body)> CallAsync(string method, string path, JToken? body = null,
            IReadOnlyDictionary<string, string>? query = null)
        {
            if (User == null)
                return (401, JToken.FromObject(new { error = "Not logged in." }));

            var context = new ApiContext(User, null, name => name switch
            {
                AccessCookie => _tokens.AccessToken,
                RefreshCookie => _tokens.RefreshToken,
                _ => null,
            });
            var answer = await _router.InvokeAsync(context, method, path, body, query);
            Keep(context.CookieChanges);
            return answer;
        }

        // The endpoint's cookie changes to the Central tokens go to the store; others (the native
        // server's local login cookie) mean nothing here.
        private void Keep(IReadOnlyList<ApiCookieChange> changes)
        {
            var access = _tokens.AccessToken;
            var refresh = _tokens.RefreshToken;
            var changed = false;
            foreach (var change in changes)
            {
                if (change.Name == AccessCookie) { access = change.Value; changed = true; }
                else if (change.Name == RefreshCookie) { refresh = change.Value; changed = true; }
            }
            if (!changed)
                return;
            if (string.IsNullOrEmpty(access))
                _tokens.Clear();
            else
                _tokens.Set(new CentralTokens(access, refresh));
        }
    }

    /// <summary>The browser host's local admin: the GM logged in to it (nobody before that).</summary>
    public sealed class HostLocalAdmin : ILocalAdminService
    {
        private readonly HostGm _host;

        public HostLocalAdmin(HostGm host) => _host = host;

        public Task<bool> IsLocalAdminAsync(string userId, string? email)
            => Task.FromResult(_host.User?.Id is { Length: > 0 } id && id == userId);

        public string? GetAutoPromotedAdminId() => _host.User?.Id;
    }
}

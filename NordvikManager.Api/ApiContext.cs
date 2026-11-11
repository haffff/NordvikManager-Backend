using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Interfaces;

namespace NordvikManager.Api
{
    /// <summary>
    /// What an endpoint may need of the request beyond its parameters: the caller, the data
    /// channel's game, and the caller's cookies (the GM's login and Central tokens).
    /// The native controllers make it from HttpContext and write <see cref="CookieChanges"/>
    /// to the response; <see cref="ApiRouter"/> makes it for a tunnelled request, which, as
    /// with the native tunnel, carries no cookies and takes none back.
    /// </summary>
    public sealed class ApiContext
    {
        private readonly Func<string, string?> _readCookie;
        private readonly List<ApiCookieChange> _cookieChanges = new();

        public ApiContext(User? user, Guid? tunnelGameId, Func<string, string?>? readCookie = null)
        {
            User = user;
            TunnelGameId = tunnelGameId;
            _readCookie = readCookie ?? (_ => null);
        }

        public User? User { get; }

        /// <summary>The game of the data channel a tunnelled request came on; null for a direct request.</summary>
        public Guid? TunnelGameId { get; }

        public IReadOnlyList<ApiCookieChange> CookieChanges => _cookieChanges;

        public string? ReadCookie(string name) => _readCookie(name);

        /// <summary>Sets one of the caller's cookies (HttpOnly, Secure, SameSite None, as all of them are).</summary>
        public void SetCookie(string name, string value) => _cookieChanges.Add(new ApiCookieChange(name, value));

        public void DeleteCookie(string name) => _cookieChanges.Add(new ApiCookieChange(name, null));

        /// <summary>
        /// Refreshed Central tokens back to the caller's cookies: the access token, and the refresh
        /// token Central rotated in (the old one is revoked), as CentralAuthCookies.WriteRefreshed.
        /// </summary>
        public void Refreshed(CentralTokens tokens)
        {
            SetCookie("CentralToken", tokens.AccessToken);
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
                SetCookie("CentralRefreshToken", tokens.RefreshToken);
        }
    }

    /// <summary>A cookie to set (<see cref="Value"/>) or delete (null).</summary>
    public sealed record ApiCookieChange(string Name, string? Value);
}

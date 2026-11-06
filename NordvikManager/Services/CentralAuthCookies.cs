using Microsoft.AspNetCore.Http;
using NordvikManager.Services.Interfaces;

namespace NordvikManager.Services
{
    /// <summary>
    /// Writes refreshed Central tokens back to the GM's cookies: the access token, and the
    /// refresh token Central rotated in (the old one is revoked, so it must be replaced).
    /// Same options as the cookies set at login.
    /// </summary>
    public static class CentralAuthCookies
    {
        public static void WriteRefreshed(HttpResponse response, CentralTokens tokens)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.None,
                Secure = true
            };
            response.Cookies.Append("CentralToken", tokens.AccessToken, options);
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
                response.Cookies.Append("CentralRefreshToken", tokens.RefreshToken, options);
        }
    }
}

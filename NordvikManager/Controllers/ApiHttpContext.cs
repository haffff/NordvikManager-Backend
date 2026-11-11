using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NordvikManager.Api;
using NordvikManager.Domain.Entities.Auth;
using System;

namespace NordvikManager.Controllers
{
    /// <summary>A shared endpoint's <see cref="ApiContext"/> from the native request, and its answer back.</summary>
    public static class ApiHttpContext
    {
        /// <summary>
        /// The caller (GetUserIntoItemsMiddleWare), the data channel's game for a tunnelled request
        /// (WebRTCInProcessDispatcher's WebRTCGameId), and the request's cookies.
        /// </summary>
        public static ApiContext ToApiContext(this HttpContext http) => new(
            http.Items["User"] as User,
            http.Items.TryGetValue("WebRTCGameId", out var value) && value is Guid gameId ? gameId : null,
            name => http.Request.Cookies[name]);

        /// <summary>The endpoint's cookie changes written to the response, then its result.</summary>
        public static IActionResult Respond(this HttpContext http, ApiContext context, ApiResult result)
        {
            foreach (var change in context.CookieChanges)
            {
                if (change.Value == null)
                    http.Response.Cookies.Delete(change.Name);
                else
                    http.Response.Cookies.Append(change.Name, change.Value, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.None,
                        Secure = true,
                    });
            }
            return result.ToActionResult();
        }
    }
}

using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Engine.Middlewares
{
    public class GetUserIntoItemsMiddleWare : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var userName = context.User?.Identity?.Name;
            if (userName == null)
            {
                await next(context);
                return;
            }

            var claims = context.User.Claims;
            var user = new User
            {
                Id = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                     ?? context.User.FindFirst("sub")?.Value
                     ?? string.Empty,
                UserName = userName,
                Email = context.User.FindFirst("email")?.Value,
                IsAdmin = context.User.FindFirst("isAdmin")?.Value?.ToLowerInvariant() == "true"
            };

            context.Items["User"] = user;
            await next(context);
        }
    }
}

using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Engine.Middlewares
{
    public class GetUserIntoItemsMiddleWare : IMiddleware
    {
        private readonly ILocalAdminService _localAdminService;

        public GetUserIntoItemsMiddleWare(ILocalAdminService localAdminService)
        {
            _localAdminService = localAdminService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var userName = context.User?.Identity?.Name;
            if (userName == null)
            {
                await next(context);
                return;
            }

            var claims = context.User.Claims;
            var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                         ?? context.User.FindFirst("sub")?.Value
                         ?? string.Empty;
            var email = context.User.FindFirst("email")?.Value;

            var isLocalAdmin = !string.IsNullOrEmpty(userId)
                && await _localAdminService.IsLocalAdminAsync(userId, email);

            var user = new User
            {
                Id = userId,
                UserName = userName,
                Email = email,
                IsAdmin = context.User.FindFirst("isAdmin")?.Value?.ToLowerInvariant() == "true",
                IsLocalAdmin = isLocalAdmin
            };

            context.Items["User"] = user;
            await next(context);
        }
    }
}

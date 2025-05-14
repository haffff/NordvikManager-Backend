using DNDOnePlaceManager.Domain.Entities.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Engine.Middlewares
{
    public class GetUserIntoItemsMiddleWare : IMiddleware
    {
        private readonly UserManager<User> userManager;

        public GetUserIntoItemsMiddleWare(UserManager<User> userManager)
        {
            this.userManager = userManager;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var userName = context.User?.Identity?.Name;
            if (userName == null)
            {
                await next(context);
                return;
            }
            var user = await userManager.FindByNameAsync(userName);
            context.Items["User"] = user;

            await next(context);
        }
    }
}

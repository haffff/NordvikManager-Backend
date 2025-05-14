using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Engine.Middlewares
{
    public class HandleExceptionMiddleWare : IMiddleware
    {

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (System.Exception ex)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { message = ex.Message });
                }
                else
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
        }
    }
}

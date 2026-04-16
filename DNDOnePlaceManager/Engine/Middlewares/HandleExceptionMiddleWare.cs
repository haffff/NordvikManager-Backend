using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Engine.Middlewares
{
    public class HandleExceptionMiddleWare : IMiddleware
    {
        private readonly ILogger<HandleExceptionMiddleWare> _logger;

        public HandleExceptionMiddleWare(ILogger<HandleExceptionMiddleWare> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                if (!context.Response.HasStarted)
                {
                    _logger.LogError(ex, "Unhandled exception on {Method} {Path} — returning 500",
                        context.Request.Method, context.Request.Path);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsJsonAsync(new { message = ex.Message });
                }
                else
                {
                    _logger.LogError(ex, "Unhandled exception on {Method} {Path} (response already started)",
                        context.Request.Method, context.Request.Path);
                }
            }
        }
    }
}

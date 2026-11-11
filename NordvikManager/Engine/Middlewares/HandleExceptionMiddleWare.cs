using NordvikManager.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Engine.Middlewares
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
                // The same statuses as the shared API router (ApiExceptions).
                var (status, level) = NordvikManager.Api.ApiExceptions.Map(ex);
                await WriteError(context, ex, status, level);
            }
        }

        private async Task WriteError(HttpContext context, Exception ex, int statusCode, LogLevel logLevel)
        {
            if (!context.Response.HasStarted)
            {
                _logger.Log(logLevel, ex, "{ExceptionType} on {Method} {Path} — returning {StatusCode}",
                    ex.GetType().Name, context.Request.Method, context.Request.Path, statusCode);
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
            else
            {
                _logger.Log(logLevel, ex, "{ExceptionType} on {Method} {Path} (response already started)",
                    ex.GetType().Name, context.Request.Method, context.Request.Path);
            }
        }
    }
}

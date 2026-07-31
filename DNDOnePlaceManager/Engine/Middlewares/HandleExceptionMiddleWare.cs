using DndOnePlaceManager.Application.Exceptions;
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
            catch (ResourceNotFoundException ex)
            {
                await WriteError(context, ex, 404, LogLevel.Warning);
            }
            catch (PermissionException ex)
            {
                await WriteError(context, ex, 403, LogLevel.Warning);
            }
            catch (WrongArgumentsException ex)
            {
                await WriteError(context, ex, 400, LogLevel.Warning);
            }
            catch (Exception ex)
            {
                await WriteError(context, ex, 500, LogLevel.Error);
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

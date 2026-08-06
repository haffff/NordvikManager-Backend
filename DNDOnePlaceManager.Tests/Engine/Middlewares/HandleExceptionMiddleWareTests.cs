using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Engine.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace DNDOnePlaceManager.Tests.Engine.Middlewares
{
    public class HandleExceptionMiddleWareTests
    {
        private readonly HandleExceptionMiddleWare _middleware = new(NullLogger<HandleExceptionMiddleWare>.Instance);

        private static DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<string> ReadBody(HttpContext context)
        {
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            return await reader.ReadToEndAsync();
        }

        [Fact]
        public async Task InvokeAsync_ResourceNotFoundException_Returns404()
        {
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => throw new ResourceNotFoundException("Game", Guid.NewGuid()));

            Assert.Equal(404, context.Response.StatusCode);
            var body = JsonDocument.Parse(await ReadBody(context));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task InvokeAsync_PermissionException_Returns403()
        {
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => throw new PermissionException(Permission.Edit));

            Assert.Equal(403, context.Response.StatusCode);
            var body = JsonDocument.Parse(await ReadBody(context));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task InvokeAsync_WrongArgumentsException_Returns400()
        {
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => throw new WrongArgumentsException("Name"));

            Assert.Equal(400, context.Response.StatusCode);
            var body = JsonDocument.Parse(await ReadBody(context));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task InvokeAsync_UnknownException_Returns500()
        {
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => throw new InvalidOperationException("boom"));

            Assert.Equal(500, context.Response.StatusCode);
            var body = JsonDocument.Parse(await ReadBody(context));
            Assert.True(body.RootElement.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task InvokeAsync_NoException_DoesNotTouchResponse()
        {
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => Task.CompletedTask);

            Assert.Equal(200, context.Response.StatusCode);
        }
    }
}

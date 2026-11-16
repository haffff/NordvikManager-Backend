using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Enums;
using NordvikManager.Engine.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Engine.Middlewares
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

        // Each exception becomes its status code, with an "error" in the JSON body.
        private static readonly Dictionary<string, (Exception Thrown, int Status)> Exceptions = new()
        {
            ["ResourceNotFound"] = (new ResourceNotFoundException("Game", Guid.NewGuid()), 404),
            ["Permission"] = (new PermissionException(Permission.Edit), 403),
            ["WrongArguments"] = (new WrongArgumentsException("Name"), 400),
            ["Unknown"] = (new InvalidOperationException("boom"), 500),
        };

        public static TheoryData<string> ExceptionNames() => new(Exceptions.Keys);

        [Theory]
        [MemberData(nameof(ExceptionNames))]
        public async Task InvokeAsync_Exception_ReturnsItsStatusWithAnError(string exception)
        {
            var (thrown, status) = Exceptions[exception];
            var context = CreateContext();

            await _middleware.InvokeAsync(context, _ => throw thrown);

            Assert.Equal(status, context.Response.StatusCode);
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

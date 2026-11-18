using Microsoft.Extensions.Logging;
using NordvikManager.Application.Exceptions;

namespace NordvikManager.Api
{
    /// <summary>
    /// The status an exception thrown by an endpoint (or a handler under it) answers with, and how
    /// loud it is logged; the native server's HandleExceptionMiddleWare uses the same.
    /// </summary>
    public static class ApiExceptions
    {
        public static (int Status, LogLevel Level) Map(Exception exception) => exception switch
        {
            ResourceNotFoundException => (404, LogLevel.Warning),
            PermissionException => (403, LogLevel.Warning),
            WrongArgumentsException => (400, LogLevel.Warning),
            UploadLimitException => (413, LogLevel.Information),
            _ => (500, LogLevel.Error),
        };
    }
}

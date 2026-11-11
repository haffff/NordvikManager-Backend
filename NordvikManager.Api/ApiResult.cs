namespace NordvikManager.Api
{
    /// <summary>Which ASP.NET Core result an <see cref="ApiResult"/> stands for (the controllers return exactly that one).</summary>
    public enum ApiResultKind
    {
        Ok,
        BadRequest,
        Unauthorized,
        Forbid,
        NotFound,
        Conflict,
        NoContent,
        Content,
        Status,
    }

    /// <summary>
    /// What an endpoint answers, without ASP.NET Core: the status, and a body when it has one.
    /// The native controllers turn it into the matching IActionResult (ToActionResult);
    /// <see cref="ApiRouter"/> writes it to the data channel as ASP.NET Core would.
    /// </summary>
    public sealed class ApiResult
    {
        private ApiResult(ApiResultKind kind, int statusCode, bool hasBody, object? body)
        {
            Kind = kind;
            StatusCode = statusCode;
            HasBody = hasBody;
            Body = body;
        }

        public ApiResultKind Kind { get; }

        public int StatusCode { get; }

        /// <summary>Whether there is a body, even a null one (Ok(null) is not Ok()).</summary>
        public bool HasBody { get; }

        public object? Body { get; }

        public static ApiResult Ok() => new(ApiResultKind.Ok, 200, false, null);
        public static ApiResult Ok(object? body) => new(ApiResultKind.Ok, 200, true, body);
        public static ApiResult BadRequest() => new(ApiResultKind.BadRequest, 400, false, null);
        public static ApiResult BadRequest(object? body) => new(ApiResultKind.BadRequest, 400, true, body);
        public static ApiResult Unauthorized() => new(ApiResultKind.Unauthorized, 401, false, null);
        public static ApiResult Unauthorized(object? body) => new(ApiResultKind.Unauthorized, 401, true, body);
        public static ApiResult Forbid() => new(ApiResultKind.Forbid, 403, false, null);
        public static ApiResult NotFound() => new(ApiResultKind.NotFound, 404, false, null);
        public static ApiResult NotFound(object? body) => new(ApiResultKind.NotFound, 404, true, body);
        public static ApiResult Conflict(object? body) => new(ApiResultKind.Conflict, 409, true, body);
        public static ApiResult NoContent() => new(ApiResultKind.NoContent, 204, false, null);

        /// <summary>Text written as is (Content(text, contentType)); the router reads it as JSON.</summary>
        public static ApiResult Content(string text, string contentType) => new(ApiResultKind.Content, 200, true, text) { ContentType = contentType };

        public string? ContentType { get; private init; }
        public static ApiResult Status(int statusCode, object? body = null) => new(ApiResultKind.Status, statusCode, body != null, body);
    }
}

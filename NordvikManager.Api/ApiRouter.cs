using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace NordvikManager.Api
{
    /// <summary>
    /// Answers the REST requests a player tunnels over the data channel from the shared endpoints
    /// (<see cref="ApiRoutes"/>), for hosts without ASP.NET Core. It does what the native
    /// tunnel gets from replaying a request through the ASP.NET Core pipeline:
    /// <list type="bullet">
    /// <item>the caller is the channel's player, as GetUserIntoItemsMiddleWare makes it;</item>
    /// <item>the session's game id replaces any the client sent;</item>
    /// <item>parameters are bound as model binding does (query, JSON body, data annotations);</item>
    /// <item>exceptions answer as HandleExceptionMiddleWare does (<see cref="ApiExceptions"/>);</item>
    /// <item>the body is written with the controllers' JSON (<see cref="ApiJson"/>), Ok(null) as 204.</item>
    /// </list>
    /// A route no endpoint has answers 501.
    /// </summary>
    public sealed class ApiRouter : IWebRTCApiDispatcher
    {
        public const string NotAvailableMessage = "This request isn't available yet in games hosted in a browser.";

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ApiRouter> _logger;
        private readonly ApiRoutes _routes;

        public ApiRouter(IServiceScopeFactory scopes, ILogger<ApiRouter> logger, ApiRoutes routes)
        {
            _scopes = scopes;
            _logger = logger;
            _routes = routes;
        }

        public async Task DispatchAsync(WebRTCApiRequest request, PlayerDTO player, Guid gameId, IPlayerConnection connection)
        {
            var (status, body) = await AnswerAsync(request, player, gameId);
            await connection.SendMessageToPlayer(new
            {
                type = "api-response",
                id = request.Id,
                status,
                body,
            });
        }

        private async Task<(int Status, JToken? Body)> AnswerAsync(WebRTCApiRequest request, PlayerDTO player, Guid gameId)
        {
            var route = _routes.Find(request.Method, request.Path);
            if (route == null)
            {
                _logger.LogWarning("Not available in this host yet: {Method} {Path} from {Player}", request.Method, request.Path, player.Name);
                return (501, Json(new { error = NotAvailableMessage }));
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var services = scope.ServiceProvider;

                // The session's game id is the authoritative one for this channel.
                var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, value) in request.Query ?? new Dictionary<string, string>())
                    query[key] = value;
                query["gameId"] = gameId.ToString();

                var user = await CallerAsync(player, services);
                if (!TryBind(route.Handler, query, request.Body, user, out var arguments, out var bindingError))
                    return (400, Json(new { error = bindingError }));

                var endpoint = ActivatorUtilities.CreateInstance(services, route.EndpointType);
                var result = await (Task<ApiResult>)route.Handler.Invoke(endpoint, arguments)!;

                // ASP.NET Core answers an Ok with a null body as 204.
                if (result.HasBody && result.Body == null && result.StatusCode == 200)
                    return (204, null);
                if (result.StatusCode >= 400)
                    _logger.LogInformation("{Method} {Path} from {Player} → {Status}", request.Method, route.Path, player.Name, result.StatusCode);
                return (result.StatusCode, result.HasBody ? Json(result.Body) : null);
            }
            catch (Exception thrown)
            {
                var exception = thrown is TargetInvocationException { InnerException: { } inner } ? inner : thrown;
                var (status, level) = ApiExceptions.Map(exception);
                _logger.Log(level, exception, "{ExceptionType} on {Method} {Path} — returning {StatusCode}",
                    exception.GetType().Name, request.Method, route.Path, status);
                return (status, Json(new { error = exception.Message }));
            }
        }

        // As GetUserIntoItemsMiddleWare makes the user from the tunnel's claims (sub, username,
        // an empty email, isAdmin false): no name, no user.
        private static async Task<User?> CallerAsync(PlayerDTO player, IServiceProvider services)
        {
            if (player.Name == null)
                return null;
            var id = player.CentralServerUserId ?? string.Empty;
            var localAdmin = services.GetService<ILocalAdminService>();
            return new User
            {
                Id = id,
                UserName = player.Name,
                Email = string.Empty,
                IsAdmin = false,
                IsLocalAdmin = localAdmin != null && !string.IsNullOrEmpty(id) && await localAdmin.IsLocalAdminAsync(id, string.Empty),
            };
        }

        private static bool TryBind(MethodInfo method, IReadOnlyDictionary<string, string> query, JToken? body, User? user,
            out object?[] arguments, out string? error)
        {
            var parameters = method.GetParameters();
            arguments = new object?[parameters.Length];
            error = null;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var type = parameter.ParameterType;
                if (type == typeof(User))
                    arguments[i] = user;
                else if (type == typeof(CancellationToken))
                    arguments[i] = CancellationToken.None;
                else if (IsSimple(type))
                {
                    if (!query.TryGetValue(parameter.Name!, out var text))
                        arguments[i] = parameter.HasDefaultValue ? parameter.DefaultValue : Default(type);
                    else if (!TryConvert(text, type, out arguments[i]))
                    {
                        error = $"The value '{text}' is not valid for {parameter.Name}.";
                        return false;
                    }
                }
                else
                {
                    if (!TryBindBody(body, type, out arguments[i], out error))
                        return false;
                }
            }
            return true;
        }

        private static bool TryBindBody(JToken? body, Type type, out object? value, out string? error)
        {
            value = null;
            error = null;
            if (body == null || body.Type == JTokenType.Null)
                return true;
            try
            {
                value = JsonSerializer.Deserialize(body.ToString(Newtonsoft.Json.Formatting.None), type, ApiJson.Options);
            }
            catch (JsonException ex)
            {
                error = "The request body is not valid: " + ex.Message;
                return false;
            }
            if (value == null)
                return true;

            // As [ApiController] answers 400 for a body failing its data annotations.
            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true))
            {
                error = string.Join(" ", results.Select(r => r.ErrorMessage));
                return false;
            }
            return true;
        }

        private static bool IsSimple(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid) || type == typeof(decimal)
                || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
        }

        private static bool TryConvert(string text, Type type, out object? value)
        {
            value = null;
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null && string.IsNullOrEmpty(text))
                return true;
            try
            {
                value = TypeDescriptor.GetConverter(underlying ?? type).ConvertFromString(null, CultureInfo.InvariantCulture, text);
                return true;
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException || ex.InnerException is FormatException or OverflowException)
            {
                return false;
            }
        }

        private static object? Default(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

        // Written as the controllers write it (System.Text.Json, ApiJson), then carried as a
        // JToken: the data channel's sender serializes with Newtonsoft, which keeps it as is.
        private static JToken? Json(object? body)
            => body == null ? null : JToken.Parse(JsonSerializer.Serialize(body, body.GetType(), ApiJson.Options));
    }
}

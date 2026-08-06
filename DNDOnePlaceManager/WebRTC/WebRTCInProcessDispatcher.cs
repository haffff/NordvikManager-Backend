using DndOnePlaceManager.Application.DataTransferObjects.Game;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Replaces WebRTCApiDispatcher. Tunnels WebRTC api-requests through the real
    /// ASP.NET Core pipeline instead of manually mapping every route.
    /// New controller routes are picked up automatically — no changes needed here.
    /// </summary>
    public class WebRTCInProcessDispatcher : IWebRTCApiDispatcher
    {
        private readonly RequestDelegateHolder _pipelineHolder;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILobbyRegistry _lobbyRegistry;
        private readonly ILogger<WebRTCInProcessDispatcher> _logger;

        public WebRTCInProcessDispatcher(
            RequestDelegateHolder pipelineHolder,
            IServiceScopeFactory scopeFactory,
            ILobbyRegistry lobbyRegistry,
            ILogger<WebRTCInProcessDispatcher> logger)
        {
            _pipelineHolder = pipelineHolder;
            _scopeFactory   = scopeFactory;
            _lobbyRegistry  = lobbyRegistry;
            _logger         = logger;
        }

        public async Task DispatchAsync(
            WebRTCApiRequest request,
            PlayerDTO player,
            Guid gameId,
            IPlayerConnection connection)
        {
            var pipeline = _pipelineHolder.Pipeline
                ?? throw new InvalidOperationException("ASP.NET Core pipeline not yet built");

            // Merge gameId into query so controller [FromQuery] params are satisfied.
            // The WebRTC client may or may not include it — inject it if absent.
            var query = new Dictionary<string, string>(
                request.Query ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            if (!query.ContainsKey("gameId"))
                query["gameId"] = gameId.ToString();

            var qs = QueryString.Create(query).Value ?? string.Empty;

            // api/materials/resource returns raw binary via File(); redirect to
            // the base64-JSON variant so the data-channel consumer can read it.
            var path = request.Path.TrimStart('/');
            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                && path.Equals("api/materials/resource", StringComparison.OrdinalIgnoreCase))
                path = "api/materials/ResourceWebRTC";

            byte[] bodyBytes = Array.Empty<byte>();
            if (request.Body != null)
                bodyBytes = Encoding.UTF8.GetBytes(request.Body.ToString(Formatting.None));

            var responseBody = new MemoryStream();

            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Method      = request.Method.ToUpperInvariant(),
                Path        = "/" + path,
                PathBase    = string.Empty,
                QueryString = qs,
                Headers     = new HeaderDictionary
                {
                    ["Content-Type"]   = "application/json",
                    ["Content-Length"] = bodyBytes.Length.ToString(),
                },
                Body = new MemoryStream(bodyBytes),
            });
            features.Set<IHttpResponseFeature>(new HttpResponseFeature());
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(responseBody));
            features.Set<IHttpConnectionFeature>(new HttpConnectionFeature
            {
                ConnectionId = Guid.NewGuid().ToString()
            });

            using var scope = _scopeFactory.CreateScope();
            var httpContext = new DefaultHttpContext(features)
            {
                RequestServices = scope.ServiceProvider
            };
            httpContext.Items["WebRTCPlayer"] = player;
            httpContext.Items["WebRTCGameId"] = gameId;

            int status;
            object? body;

            try
            {
                await pipeline(httpContext);
                await httpContext.Response.BodyWriter.FlushAsync();

                status = httpContext.Response.StatusCode == 0 ? 200 : httpContext.Response.StatusCode;

                responseBody.Seek(0, SeekOrigin.Begin);
                var json = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync();
                body = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonConvert.DeserializeObject<object?>(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebRTC in-process {Method} {Path}", request.Method, request.Path);
                status = 500;
                body   = new { error = ex.Message };
            }

            _logger.LogDebug("WebRTC in-process {Method} {Path} → {Status} id={Id}",
                request.Method, request.Path, status, request.Id);

            if (status >= 400)
                TryLogToEventLog(gameId, player, request.Method, request.Path, status, body);

            await connection.SendMessageToPlayer(new
            {
                type   = "api-response",
                id     = request.Id,
                status,
                body
            });
        }

        private void TryLogToEventLog(
            Guid gameId, PlayerDTO player, string method, string path, int status, object? body)
        {
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            if (path.Contains("eventlog", StringComparison.OrdinalIgnoreCase))
                return;

            var level    = status >= 500 || status == 403 ? "Error" : "Warning";
            var category = DetermineCategory(path);
            var message  = ExtractErrorMessage(body);

            lobby.EventLog.Log(level, category,
                $"[{method.ToUpperInvariant()} {path}] {message}",
                player?.Name,
                new { path, method, status });
        }

        private static string DetermineCategory(string path)
        {
            if (path.Contains("/addon/",      StringComparison.OrdinalIgnoreCase)) return "Addon";
            if (path.Contains("/materials/",  StringComparison.OrdinalIgnoreCase)) return "Materials";
            if (path.Contains("/map/",        StringComparison.OrdinalIgnoreCase)) return "Map";
            if (path.Contains("/battlemap/",  StringComparison.OrdinalIgnoreCase)) return "BattleMap";
            if (path.Contains("/properties/", StringComparison.OrdinalIgnoreCase)) return "Properties";
            if (path.Contains("/security/",   StringComparison.OrdinalIgnoreCase)) return "Permission";
            return "API";
        }

        private static string ExtractErrorMessage(object? body)
        {
            if (body == null) return "Unknown error";
            if (body is string s) return s;
            if (body is JObject jObj && jObj["error"] is JToken err) return err.ToString();
            var prop = body.GetType().GetProperty("error");
            return prop?.GetValue(body)?.ToString() ?? body.ToString() ?? "Unknown error";
        }
    }
}

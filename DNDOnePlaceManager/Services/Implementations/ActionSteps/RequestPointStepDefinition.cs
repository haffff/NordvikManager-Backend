using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Asks a player to point at a place on their battle map and waits. A marker (built like
    /// ShowMarker's) follows the cursor, everyone sees it move, tokens it covers glow, and it
    /// can snap to the grid and be kept within range of a token or point. The answer is
    /// checked here too: a point on this map, within range.
    /// </summary>
    public class RequestPointStepDefinition : IActionStepDefinition
    {
        public const string Command = "request_point";

        // Rounding on the way (snapping, pixels) shouldn't turn down an answer the client allowed.
        private const double RangeTolerance = 1;

        public string Name => "Request Point";
        public string Value => "RequestPoint";
        public IReadOnlyList<string> Keywords => new[] { "point", "where", "aim", "area", "place", "position", "click the map", "spell" };
        public string Category => "Control Flow";
        public string Description => "Asks a player to point at a place on the map (e.g. where a Fireball goes off) and waits. A marker follows " +
            "the cursor for everyone to see, and tokens it covers glow. Cancelling stops the action unless Stop If Cancelled is off.";
        public string? Summary => "Ask[ {Player}] to point[ a {Shape}][ within {MaxDistance}][: {Message}][ → {Output}]";
        public Type DataType => typeof(RequestPointStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<RequestPointStepData>()!;
            if (string.IsNullOrWhiteSpace(data.Output))
                throw new ActionProcessException("RequestPoint: 'Output' is required: the variable for the point.");

            var snap = (data.SnapToGrid ?? "").Trim().ToLowerInvariant() switch
            {
                "" or "none" => "none",
                "center" => "center",
                "corner" => "corner",
                _ => throw new ActionProcessException($"RequestPoint: 'SnapToGrid' must be none, center or corner, not '{data.SnapToGrid}'."),
            };
            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "RequestPoint", data.MapId);
            var grid = await MapShape.GridSizeAsync(mediator, gameLobby, mapId);

            JObject? marker = null;
            if (!string.Equals(data.Shape?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                marker = MapShape.Build("RequestPoint", data.Shape, null, null, data.Radius, data.Width, data.Height, data.Color, data.Text, grid);
                marker["selectable"] = false;
                marker["evented"] = false;
            }
            var range = await RangeAsync(mediator, gameLobby, mapId, data, grid);

            var reply = await PlayerRequest.AskAsync(gameLobby, variables, data.Player, Command, new JObject
            {
                ["message"] = data.Message ?? "",
                ["mapId"] = mapId.ToString(),
                ["marker"] = marker,
                ["snap"] = snap,
                ["gridSize"] = grid,
                ["range"] = range,
                ["highlight"] = data.HighlightTokens,
                ["showToOthers"] = data.ShowToOthers,
            }, data.Timeout);

            var point = Accept(reply, mapId, range);
            if (point == null)
            {
                if (data.StopIfCancelled)
                    throw new ActionExitException(null);
                variables[data.Output] = JValue.CreateNull();
                return;
            }
            variables[data.Output] = point;
        }

        // { x, y, radius } in map pixels; null when there's no limit.
        private static async Task<JObject?> RangeAsync(IMediator mediator, GameLobby gameLobby, Guid mapId, RequestPointStepData data, double grid)
        {
            if (string.IsNullOrWhiteSpace(data.MaxDistance))
                return null;
            if (string.IsNullOrWhiteSpace(data.From))
                throw new ActionProcessException("RequestPoint: 'MaxDistance' needs 'From': the token or point the range counts from.");
            var squares = MapShape.Number("RequestPoint", data.MaxDistance, "MaxDistance", 0);

            var from = data.From.Trim();
            JObject? given = null;
            if (from.StartsWith("{"))
            {
                try { given = JObject.Parse(from); }
                catch (JsonException) { throw new ActionProcessException($"RequestPoint: 'From' isn't a token or a point: '{from}'."); }
                if (given["id"] == null)
                {
                    if (!IsNumber(given["x"]) || !IsNumber(given["y"]))
                        throw new ActionProcessException("RequestPoint: 'From' as a point needs a number x and y, e.g. %position%.");
                    return new JObject { ["x"] = (double)given["x"]!, ["y"] = (double)given["y"]!, ["radius"] = squares * grid };
                }
            }

            var id = (string?)given?["id"] ?? from;
            var token = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "tokens", MapId = mapId }) ?? new List<JObject>())
                .FirstOrDefault(t => (string?)t["id"] == id)
                ?? throw new ActionProcessException($"RequestPoint: there is no token '{id}' on this map to count the range from.");
            return new JObject
            {
                ["x"] = (double)token["x"]!,
                ["y"] = (double)token["y"]!,
                ["radius"] = squares * grid + Math.Min((double)token["w"]!, (double)token["h"]!) / 2,
            };
        }

        // The point the player gave, when it's a point on this map within range; else null
        // (cancelled, not answered in time, or not a valid answer).
        private static JObject? Accept(JToken? reply, Guid mapId, JObject? range)
        {
            if (reply is not JObject answer || !IsNumber(answer["x"]) || !IsNumber(answer["y"]))
                return null;
            if ((string?)answer["mapId"] != mapId.ToString())
                return null;
            double x = (double)answer["x"]!, y = (double)answer["y"]!;
            if (!double.IsFinite(x) || !double.IsFinite(y))
                return null;
            if (range != null)
            {
                double dx = x - (double)range["x"]!, dy = y - (double)range["y"]!;
                if (Math.Sqrt(dx * dx + dy * dy) > (double)range["radius"]! + RangeTolerance)
                    return null;
            }
            return new JObject { ["x"] = x, ["y"] = y, ["mapId"] = mapId.ToString() };
        }

        private static bool IsNumber(JToken? value) => value?.Type is JTokenType.Integer or JTokenType.Float;
    }
}

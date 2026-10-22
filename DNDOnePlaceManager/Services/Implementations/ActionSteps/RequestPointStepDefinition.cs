using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Asks a player to point at a place on their battle map and waits. The area (the same
    /// shapes as ShowMarker and Find) follows the cursor for everyone to see, tokens it covers
    /// glow, and it can snap to the grid and be kept within range. A line or cone starts at
    /// From and the player only aims it. The answer is checked here too: a point on this map,
    /// within range.
    /// </summary>
    public class RequestPointStepDefinition : IActionStepDefinition
    {
        public const string Command = "request_point";

        // Rounding on the way (snapping, pixels) shouldn't turn down an answer the client allowed.
        private const double RangeTolerance = 1;

        public string Name => "Request Point";
        public string Value => "RequestPoint";
        public IReadOnlyList<string> Keywords => new[] { "point", "where", "aim", "area", "place", "position", "click the map", "spell", "cone", "line" };
        public string Category => "Control Flow";
        public string Description => "Asks a player to point at a place on the map (e.g. where a Fireball goes off, or which way Burning Hands " +
            "goes) and waits. The area follows the cursor for everyone to see, and tokens it covers glow. Cancelling stops the action " +
            "unless Stop If Cancelled is off.";
        public string? Summary => "Ask[ {Player}] to point[ a {Shape}][ from {From}][ within {MaxDistance}][: {Message}][ → {Output}]";
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
            var none = string.Equals(data.Shape?.Trim(), "none", StringComparison.OrdinalIgnoreCase);
            var shape = none ? "none" : MapShape.ShapeOf("RequestPoint", data.Shape);
            var aimed = shape is "line" or "cone";
            if (aimed && string.IsNullOrWhiteSpace(data.From))
                throw new ActionProcessException($"RequestPoint: a {shape} starts at 'From' (the caster's token, or a point); the player aims it.");
            if (aimed && !string.IsNullOrWhiteSpace(data.MaxDistance))
                throw new ActionProcessException($"RequestPoint: a {shape} is as long as its Radius, so it has no 'MaxDistance'.");
            if (!aimed && !string.IsNullOrWhiteSpace(data.MaxDistance) && string.IsNullOrWhiteSpace(data.From))
                throw new ActionProcessException("RequestPoint: 'MaxDistance' needs 'From': the token or point the range counts from.");

            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "RequestPoint", data.MapId);
            var grid = await MapShape.GridSizeAsync(mediator, gameLobby, mapId);

            (double X, double Y, double W, double H)? from = string.IsNullOrWhiteSpace(data.From)
                ? null
                : await MapPlace.Parse("RequestPoint", "From", data.From).ResolveAsync(mediator, gameLobby, "RequestPoint", "From");

            JObject? area = null;
            if (!none)
            {
                var spec = new MapShape.Spec(shape, null, null, data.Radius, data.Width, data.Height, data.Angle, data.Color, data.Text);
                var size = MapShape.AreaOf("RequestPoint", shape == "text" ? "rect" : shape, spec, 0, 0, null, grid);
                var colours = MapShape.OwnColours(data.Color, variables);
                area = new JObject
                {
                    ["shape"] = shape,
                    ["radius"] = size.Radius,
                    ["width"] = size.Width,
                    ["height"] = size.Height,
                    ["angle"] = size.Angle,
                    ["text"] = data.Text ?? "",
                    ["fontSize"] = grid * 0.6,
                    // The action's colour or the player's; null: the map's own (the theme's, else orange).
                    ["fill"] = colours == null ? JValue.CreateNull() : colours.Value.Fill,
                    ["stroke"] = colours == null ? JValue.CreateNull() : colours.Value.Stroke,
                };
            }

            // A range around From, in map pixels; from a token it counts from its edge, like Find.
            JObject? range = null;
            if (!aimed && from is { } f && !string.IsNullOrWhiteSpace(data.MaxDistance))
                range = new JObject
                {
                    ["x"] = f.X,
                    ["y"] = f.Y,
                    ["radius"] = MapShape.Number("RequestPoint", data.MaxDistance, "MaxDistance", 0) * grid + Math.Min(f.W, f.H) / 2,
                };
            var start = from is { } s ? new JObject { ["x"] = s.X, ["y"] = s.Y } : null;

            var reply = await PlayerRequest.AskAsync(gameLobby, variables, data.Player, Command, new JObject
            {
                ["message"] = data.Message ?? "",
                ["mapId"] = mapId.ToString(),
                ["area"] = area,
                ["from"] = aimed ? start : null,
                ["snap"] = snap,
                ["gridSize"] = grid,
                ["range"] = range,
                ["highlight"] = data.HighlightTokens,
                ["showToOthers"] = data.ShowToOthers,
            }, data.Timeout);

            var point = Accept(reply, mapId, range);
            if (point != null && aimed)
                point = AxisEnd(point, from!.Value, (double)area!["radius"]!);
            if (point == null)
            {
                if (data.StopIfCancelled)
                    throw new ActionExitException(null);
                variables[data.Output] = JValue.CreateNull();
                return;
            }
            point["from"] = start;
            variables[data.Output] = point;
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

        // A line or cone is aimed: only the direction from its start counts, and the point is
        // the end of its axis. Aiming at the start itself points nowhere.
        private static JObject? AxisEnd(JObject point, (double X, double Y, double W, double H) from, double length)
        {
            double dx = (double)point["x"]! - from.X, dy = (double)point["y"]! - from.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-9)
                return null;
            point["x"] = from.X + dx / d * length;
            point["y"] = from.Y + dy / d * length;
            return point;
        }

        private static bool IsNumber(JToken? value) => value?.Type is JTokenType.Integer or JTokenType.Float;
    }
}

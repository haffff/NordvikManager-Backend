using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// A shape as the map's canvas (fabric.js) draws it, for ShowMarker and AddElement: a circle
    /// or rect centred on At, a line or cone from At toward Toward (drawn as the area Find uses,
    /// <see cref="AreaShape"/>), or text. Sizes are in squares of the map.
    /// </summary>
    public static class MapShape
    {
        public static readonly string[] Shapes = { "circle", "rect", "line", "cone", "text" };

        private const string DefaultFill = "rgba(255,120,0,0.35)";
        private const string DefaultStroke = "rgba(255,255,255,0.8)";

        /// <summary>A shape's arguments, as the steps take them (sizes in squares).</summary>
        public sealed record Spec(string? Shape, string? At, string? Toward, string? Radius, string? Width, string? Height,
            string? Angle, string? Color, string? Text);

        public static async Task<double> GridSizeAsync(IMediator mediator, GameLobby gameLobby, Guid mapId)
        {
            var map = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "maps", Id = mapId }))?.FirstOrDefault();
            return (double?)map?["gridSize"] is > 0 ? (double)map!["gridSize"]! : 50;
        }

        /// <summary>circle, rect, line, cone or text (default circle); an error for anything else.</summary>
        public static string ShapeOf(string step, string? shape)
        {
            var s = string.IsNullOrWhiteSpace(shape) ? "circle" : shape.Trim().ToLowerInvariant();
            return Shapes.Contains(s) ? s : throw new ActionProcessException($"{step}: 'Shape' must be {string.Join(", ", Shapes)}, not '{shape}'.");
        }

        /// <summary>The area of a circle, rect, line or cone in map pixels, at a point (and toward one).</summary>
        public static Area AreaOf(string step, string shape, Spec spec, double x, double y, (double X, double Y)? toward, double grid) =>
            new(shape, x, y,
                Number(step, spec.Radius, "Radius", shape is "line" or "cone" ? 3 : 1) * grid,
                Number(step, spec.Width, "Width", 1) * grid,
                Number(step, spec.Height, "Height", 1) * grid,
                Number(step, spec.Angle, "Angle", 53),
                toward?.X, toward?.Y);

        /// <summary>The canvas object of a shape whose At (and Toward) are given.</summary>
        public static async Task<JObject> BuildAsync(IMediator mediator, GameLobby gameLobby, Dictionary<string, object> variables,
            string step, Spec spec, double grid)
        {
            var shape = ShapeOf(step, spec.Shape);
            if (string.IsNullOrWhiteSpace(spec.At))
                throw new ActionProcessException($"{step}: 'At' is required: where the {shape} goes, e.g. %point% or a token." + MapPlace.EmptyHint);
            var at = await MapPlace.Parse(step, "At", spec.At).ResolveAsync(mediator, gameLobby, step, "At");

            (double X, double Y)? toward = null;
            if (shape is "line" or "cone")
            {
                if (string.IsNullOrWhiteSpace(spec.Toward))
                    throw new ActionProcessException($"{step}: a {shape} needs 'Toward': the point or token it points at." + MapPlace.EmptyHint);
                var t = await MapPlace.Parse(step, "Toward", spec.Toward).ResolveAsync(mediator, gameLobby, step, "Toward");
                if (t.X == at.X && t.Y == at.Y)
                    throw new ActionProcessException($"{step}: 'Toward' is where the {shape} starts, so it points nowhere.");
                toward = (t.X, t.Y);
            }

            var (fill, stroke) = Colours(spec.Color, variables);
            var o = new JObject { ["scaleX"] = 1, ["scaleY"] = 1, ["fill"] = fill, ["stroke"] = stroke, ["strokeWidth"] = 2 };
            switch (shape)
            {
                case "text":
                    o["type"] = "text";
                    o["left"] = at.X;
                    o["top"] = at.Y;
                    o["originX"] = "center";
                    o["originY"] = "center";
                    o["text"] = spec.Text ?? "";
                    o["fontSize"] = grid * 0.6;
                    o["fill"] = string.IsNullOrWhiteSpace(spec.Color) ? "white" : spec.Color.Trim();
                    o["stroke"] = "black";
                    o["strokeWidth"] = 1;
                    return o;
                case "line":
                case "cone":
                    // Its box too (as fabric works it out from the points), so the server knows where
                    // it is (Find) before any browser has drawn it.
                    var corners = AreaShape.Polygon(AreaOf(step, shape, spec, at.X, at.Y, toward, grid));
                    double minX = corners.Min(p => p.X), minY = corners.Min(p => p.Y);
                    o["type"] = "polygon";
                    o["points"] = new JArray(corners.Select(p => new JObject { ["x"] = p.X, ["y"] = p.Y }));
                    o["originX"] = "left";
                    o["originY"] = "top";
                    o["left"] = minX - 1; // half the stroke, as fabric does
                    o["top"] = minY - 1;
                    o["width"] = corners.Max(p => p.X) - minX;
                    o["height"] = corners.Max(p => p.Y) - minY;
                    return o;
                default:
                    var area = AreaOf(step, shape, spec, at.X, at.Y, null, grid);
                    o["type"] = shape;
                    o["left"] = at.X;
                    o["top"] = at.Y;
                    o["originX"] = "center";
                    o["originY"] = "center";
                    if (shape == "circle")
                        o["radius"] = area.Radius;
                    else
                    {
                        o["width"] = area.Width;
                        o["height"] = area.Height;
                    }
                    return o;
            }
        }

        /// <summary>
        /// Fill and outline: the action's Color as the fill; else the colour of the player whose
        /// action it is (see-through inside, as their measuring); else the default orange.
        /// </summary>
        public static (string Fill, string Stroke) Colours(string? color, Dictionary<string, object>? variables) =>
            OwnColours(color, variables) ?? (DefaultFill, DefaultStroke);

        /// <summary>The action's colour or the player's, as in <see cref="Colours"/>; null when neither is there.</summary>
        public static (string Fill, string Stroke)? OwnColours(string? color, Dictionary<string, object>? variables)
        {
            if (!string.IsNullOrWhiteSpace(color))
                return (color.Trim(), DefaultStroke);
            var player = variables != null && variables.TryGetValue("Player", out var p) ? p as PlayerDTO : null;
            if (Rgb(player?.Color) is var (r, g, b))
                return ($"rgba({r},{g},{b},0.25)", $"rgb({r},{g},{b})");
            return null;
        }

        /// <summary>A CSS colour as #rgb, #rrggbb, rgb(…) or rgba(…); null for anything else, or fully see-through.</summary>
        public static (int R, int G, int B)? Rgb(string? color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return null;
            var c = color.Trim();
            var hex = Regex.Match(c, "^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
            if (hex.Success)
            {
                var h = hex.Groups[1].Value;
                if (h.Length == 3)
                    h = string.Concat(h.Select(ch => $"{ch}{ch}"));
                return (Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..], 16));
            }
            var rgb = Regex.Match(c, @"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*([\d.]+)\s*)?\)$");
            if (rgb.Success)
            {
                // Fully see-through: what a player without a colour has.
                if (rgb.Groups[4].Success && double.TryParse(rgb.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var alpha) && alpha == 0)
                    return null;
                var parts = new[] { 1, 2, 3 }.Select(i => int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray();
                if (parts.All(v => v <= 255))
                    return (parts[0], parts[1], parts[2]);
            }
            return null;
        }

        public static double Number(string step, string? value, string argument, double fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : ElementStepHelper.ParseNumber(step, argument, value);
    }
}

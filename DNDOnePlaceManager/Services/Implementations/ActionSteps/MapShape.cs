using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Exceptions;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// A circle, rectangle or text as the map's canvas (fabric.js) draws it, centred on X, Y
    /// (map pixels) with sizes in squares of the map. Used by ShowMarker and AddElement.
    /// </summary>
    public static class MapShape
    {
        public static async Task<double> GridSizeAsync(IMediator mediator, GameLobby gameLobby, Guid mapId)
        {
            var map = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "maps", Id = mapId }))?.FirstOrDefault();
            return (double?)map?["gridSize"] is > 0 ? (double)map!["gridSize"]! : 50;
        }

        public static JObject Build(string step, string? shape, string? x, string? y, string? radius, string? width, string? height,
            string? color, string? text, double grid)
        {
            var fill = string.IsNullOrWhiteSpace(color) ? "rgba(255,120,0,0.35)" : color.Trim();
            var o = new JObject
            {
                ["left"] = Number(step, x, "X", 0),
                ["top"] = Number(step, y, "Y", 0),
                ["originX"] = "center",
                ["originY"] = "center",
                ["scaleX"] = 1,
                ["scaleY"] = 1,
            };
            switch ((shape ?? "circle").Trim().ToLowerInvariant())
            {
                case "circle":
                    o["type"] = "circle";
                    o["radius"] = Number(step, radius, "Radius", 1) * grid;
                    o["fill"] = fill;
                    o["stroke"] = "rgba(255,255,255,0.8)";
                    o["strokeWidth"] = 2;
                    return o;
                case "rect":
                    o["type"] = "rect";
                    o["width"] = Number(step, width, "Width", 1) * grid;
                    o["height"] = Number(step, height, "Height", 1) * grid;
                    o["fill"] = fill;
                    o["stroke"] = "rgba(255,255,255,0.8)";
                    o["strokeWidth"] = 2;
                    return o;
                case "text":
                    o["type"] = "text";
                    o["text"] = text ?? "";
                    o["fontSize"] = grid * 0.6;
                    o["fill"] = string.IsNullOrWhiteSpace(color) ? "white" : fill;
                    o["stroke"] = "black";
                    o["strokeWidth"] = 1;
                    return o;
                default:
                    throw new ActionProcessException($"{step}: 'Shape' must be circle, rect or text, not '{shape}'.");
            }
        }

        public static double Number(string step, string? value, string argument, double fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : ElementStepHelper.ParseNumber(step, argument, value);
    }
}

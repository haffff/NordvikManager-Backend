using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// A shape everyone sees on a map for a while — where a spell lands, an area — sent like
    /// a sketch (preview_start / preview_end) to every battle map view showing the map, so it
    /// is never saved. It goes away after Duration, or when the action ends.
    /// </summary>
    public class ShowMarkerStepDefinition : IActionStepDefinition
    {
        public string Name => "Show Marker";
        public string Value => "ShowMarker";
        public IReadOnlyList<string> Keywords => new[] { "marker", "area", "circle", "template", "highlight", "show on map", "effect" };
        public string Category => "Map";
        public string Description => "Shows a circle, rectangle or text on the map to everyone for a while (e.g. where a Fireball goes off). It is never saved.";
        public string? Summary => "Show a {Shape}[ of radius {Radius}] at {X}, {Y}[ → {Output}]";
        public Type DataType => typeof(ShowMarkerStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<ShowMarkerStepData>()!;
            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "ShowMarker", data.MapId);
            var map = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "maps", Id = mapId }))?.FirstOrDefault();
            var grid = (double?)map?["gridSize"] is > 0 ? (double)map!["gridSize"]! : 50;

            var markerId = Guid.NewGuid().ToString();
            var shape = Shape(data, grid);
            shape["previewId"] = markerId;
            shape["playerId"] = gameLobby.SystemPlayer.Id.ToString();
            shape["stayVisible"] = true;
            shape["selectable"] = false;
            shape["evented"] = false;

            var views = (await mediator.Send(new GetBattleMapsCommand { GameID = gameLobby.GameId, Player = gameLobby.SystemPlayer }) ?? new())
                .Where(v => v.MapId == mapId && v.Id != null).Select(v => v.Id!.Value.ToString()).ToList();
            Send(gameLobby, views, WebSocketCommandNames.CmdPreviewStart, new JArray(shape));

            Func<Task> hide = () =>
            {
                Send(gameLobby, views, WebSocketCommandNames.CmdPreviewEnd, new JArray(new JObject { ["previewId"] = markerId }));
                return Task.CompletedTask;
            };
            if (data.UntilActionEnds)
                gameLobby.ActionProcessingService.AtActionEnd(hide);
            else
                _ = Task.Delay(TimeSpan.FromSeconds(Number(data.Duration, "Duration", 10))).ContinueWith(_ => hide());

            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = markerId;
        }

        private static void Send(GameLobby gameLobby, List<string> views, string command, JArray data)
        {
            foreach (var view in views)
                gameLobby.Broadcast(new WebSocketCommand
                {
                    Command = command,
                    BattleMapId = view,
                    PlayerId = gameLobby.SystemPlayer.Id,
                    Data = data,
                }, gameLobby.SystemPlayer);
        }

        // The fabric object, centred on X, Y; sizes come in squares.
        private static JObject Shape(ShowMarkerStepData data, double grid)
        {
            var x = Number(data.X, "X", 0);
            var y = Number(data.Y, "Y", 0);
            var color = string.IsNullOrWhiteSpace(data.Color) ? "rgba(255,120,0,0.35)" : data.Color.Trim();
            var common = new JObject { ["left"] = x, ["top"] = y, ["originX"] = "center", ["originY"] = "center" };
            switch ((data.Shape ?? "circle").Trim().ToLowerInvariant())
            {
                case "circle":
                    common["type"] = "circle";
                    common["radius"] = Number(data.Radius, "Radius", 1) * grid;
                    common["fill"] = color;
                    common["stroke"] = "rgba(255,255,255,0.8)";
                    common["strokeWidth"] = 2;
                    return common;
                case "rect":
                    common["type"] = "rect";
                    common["width"] = Number(data.Width, "Width", 1) * grid;
                    common["height"] = Number(data.Height, "Height", 1) * grid;
                    common["fill"] = color;
                    common["stroke"] = "rgba(255,255,255,0.8)";
                    common["strokeWidth"] = 2;
                    return common;
                case "text":
                    common["type"] = "text";
                    common["text"] = data.Text ?? "";
                    common["fontSize"] = grid * 0.6;
                    common["fill"] = string.IsNullOrWhiteSpace(data.Color) ? "white" : color;
                    common["stroke"] = "black";
                    common["strokeWidth"] = 1;
                    return common;
                default:
                    throw new ActionProcessException($"ShowMarker: 'Shape' must be circle, rect or text, not '{data.Shape}'.");
            }
        }

        private static double Number(string? value, string argument, double fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : ElementStepHelper.ParseNumber("ShowMarker", argument, value);
    }
}

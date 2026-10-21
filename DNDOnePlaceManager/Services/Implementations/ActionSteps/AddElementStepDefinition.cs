using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Adds a circle, rectangle or text to a map, saved like an element a player drew: it goes
    /// through element_add as the system player, so everyone sees it and the hooks fire.
    /// </summary>
    public class AddElementStepDefinition : IActionStepDefinition
    {
        private const int MapLayer = -100;

        public string Name => "Add Element";
        public string Value => "AddElement";
        public IReadOnlyList<string> Keywords => new[] { "draw", "shape", "circle", "wall", "area", "zone", "text on map", "add to map" };
        public string Category => "Map";
        public string Description => "Adds a circle, rectangle or text to a map, saved like one a player drew (e.g. a wall of fire). " +
            "For a shape that only shows for a while, use Show Marker.";
        public string? Summary => "Add a {Shape} at {X}, {Y}[ → {Output}]";
        public Type DataType => typeof(AddElementStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<AddElementStepData>()!;
            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "AddElement", data.MapId);
            var grid = await MapShape.GridSizeAsync(mediator, gameLobby, mapId);
            var shape = MapShape.Build("AddElement", data.Shape, data.X, data.Y, data.Radius, data.Width, data.Height, data.Color, data.Text, grid);

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.ElementAdd,
                GameId = gameLobby.GameId,
                Data = new JObject
                {
                    ["object"] = shape.ToString(Newtonsoft.Json.Formatting.None),
                    ["mapID"] = mapId,
                    ["layer"] = (int)MapShape.Number("AddElement", data.Layer, "Layer", MapLayer),
                },
            };
            await ElementStepHelper.SendAsync(gameLobby, command, "AddElement");

            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = command.Data["id"]?.ToString();
        }
    }
}

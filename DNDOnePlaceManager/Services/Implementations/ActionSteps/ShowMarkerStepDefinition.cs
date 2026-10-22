using DndOnePlaceManager.Application.Commands.BattleMap;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
        public string Description => "Shows a circle, rectangle, line, cone or text on the map to everyone for a while (e.g. where a Fireball goes off). " +
            "It is never saved.";
        public string? Summary => "Show a {Shape}[ of {Radius}] at {At}[ toward {Toward}][ → {Output}]";
        public Type DataType => typeof(ShowMarkerStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<ShowMarkerStepData>()!;
            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "ShowMarker", data.MapId);
            var grid = await MapShape.GridSizeAsync(mediator, gameLobby, mapId);

            var markerId = Guid.NewGuid().ToString();
            var shape = await MapShape.BuildAsync(mediator, gameLobby, variables, "ShowMarker",
                new MapShape.Spec(data.Shape, data.At, data.Toward, data.Radius, data.Width, data.Height, data.Angle, data.Color, data.Text), grid);
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
                _ = Task.Delay(TimeSpan.FromSeconds(MapShape.Number("ShowMarker", data.Duration, "Duration", 10))).ContinueWith(_ => hide());

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
    }
}

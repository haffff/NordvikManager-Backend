using NordvikManager.Application.Commands.Actions.BuildToken;
using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// Places a token on a map from the server, with nobody's browser involved (hooks, the GM
    /// away): built like the client builds one (BuildTokenCommand), added through element_add
    /// as the system player so everyone sees it and the hooks fire.
    /// </summary>
    public class SpawnTokenStepDefinition : IActionStepDefinition
    {
        public string Name => "Spawn Token";
        public string Value => "SpawnToken";
        public IReadOnlyList<string> Keywords => new[] { "place", "token", "monster", "summon", "put on map", "creature" };
        public string Category => "Map";
        public string Description => "Places a token of a card (or from a token file) on a map. Works from hooks too: no player's browser is needed.";
        public string? Summary => "Place a token[ of {CardId}][ {Token}] at {X}, {Y}[ → {Output}]";
        public Type DataType => typeof(SpawnTokenStepData);

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<SpawnTokenStepData>()!;
            var mapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "SpawnToken", data.MapId);

            BuiltToken token;
            try
            {
                token = await mediator.Send(new BuildTokenCommand
                {
                    GameId = gameLobby.GameId,
                    Player = gameLobby.SystemPlayer,
                    MapId = mapId,
                    CardId = string.IsNullOrWhiteSpace(data.CardId) ? null : ElementStepHelper.ParseGuid("SpawnToken", "CardId", data.CardId),
                    Token = data.Token,
                    Image = data.Image,
                    X = MapShape.Number("SpawnToken", data.X, "X", 0),
                    Y = MapShape.Number("SpawnToken", data.Y, "Y", 0),
                });
            }
            catch (ArgumentException e)
            {
                throw new ActionProcessException($"SpawnToken: {e.Message}");
            }

            var command = new WebSocketCommand
            {
                Command = WebSocketCommandNames.ElementAdd,
                GameId = gameLobby.GameId,
                Data = new JObject
                {
                    ["object"] = token.Object.ToString(Newtonsoft.Json.Formatting.None),
                    ["mapId"] = mapId,
                    ["layer"] = (int)token.Object["layer"]!,
                    ["properties"] = new JArray(
                        new JObject { ["name"] = "tokenSize", ["value"] = token.Size.ToString() },
                        new JObject { ["name"] = "isToken", ["value"] = "true" }),
                },
            };
            await ElementStepHelper.SendAsync(gameLobby, command, "SpawnToken");

            if (!string.IsNullOrWhiteSpace(data.Output))
                variables[data.Output] = command.Data["id"]?.ToString();
        }
    }
}

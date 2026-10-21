using DndOnePlaceManager.Application.Commands.Actions.Find;
using DndOnePlaceManager.Application.Exceptions;
using DNDOnePlaceManager.Services.Implementations.ActionBody;
using DNDOnePlaceManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations.ActionSteps
{
    public class FindStepDefinition : IActionStepDefinition
    {
        public string Name => "Find";
        public string Value => "Find";
        public IReadOnlyList<string> Keywords => new[] { "tokens", "creatures", "monsters", "targets", "cards", "characters", "maps", "players", "search", "get", "list", "near", "in range" };
        public string Category => "Data";
        public string Description => "Finds the game's tokens, cards, maps, players… and stores them as a list (or the first one). " +
            "Tokens are looked for on the map the player is looking at unless you say otherwise.";
        public string? Summary => "Find {What}[ named {Name}][ with {Property}][ near {Near}][ → {Output}]";
        public Type DataType => typeof(FindStepData);

        private static readonly string[] OnMaps = { "tokens", "elements" };

        public async Task Execute(IMediator mediator, Dictionary<string, object> variables, GameLobby gameLobby, ActionStep step)
        {
            var data = step.Data.ToObject<FindStepData>()!;
            if (string.IsNullOrWhiteSpace(data.What) || !FindShapes.Fields.ContainsKey(data.What.Trim()))
                throw new ActionProcessException($"Find: 'What' must be one of {string.Join(", ", FindShapes.Fields.Keys)}.");
            if (string.IsNullOrWhiteSpace(data.Output))
                throw new ActionProcessException("Find: 'Output' is required: the variable to store what was found.");

            var what = FindShapes.Fields.Keys.First(k => string.Equals(k, data.What.Trim(), StringComparison.OrdinalIgnoreCase));
            var command = new FindCommand
            {
                GameId = gameLobby.GameId,
                What = what,
                Id = OptionalGuid(data.Id, "Id"),
                Name = Blank(data.Name),
                Property = Blank(data.Property),
                WithCard = data.WithCard,
                IncludeTemplates = data.IncludeTemplates,
                Radius = OptionalNumber(data.Radius, "Radius"),
            };

            if (OnMaps.Contains(what) && !data.AllMaps && command.Id == null)
                command.MapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "Find", data.MapId);

            // The battle map view "first" means the one the player is looking at.
            if (what == "battleMaps" && data.First && command.Id == null && command.Name == null)
                command.Id = (await ActiveMap.BattleMapAsync(mediator, gameLobby, variables, null))?.Id;

            if (!string.IsNullOrWhiteSpace(data.Near))
                SetNear(command, data.Near.Trim());

            var found = await mediator.Send(command) ?? new List<JObject>();
            variables[data.Output] = data.First ? found.FirstOrDefault() : new JArray(found);
        }

        // A point as JSON ({"x":1,"y":2}, what %position% gives) or "x,y", or an element's id.
        private static void SetNear(FindCommand command, string near)
        {
            if (Guid.TryParse(near, out var element))
            {
                command.NearElementId = element;
                return;
            }
            if (near.StartsWith("{"))
            {
                try
                {
                    var point = JObject.Parse(near);
                    command.NearX = (double?)point["x"];
                    command.NearY = (double?)point["y"];
                    if (command.NearX != null && command.NearY != null)
                        return;
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            var parts = near.Split(',');
            if (parts.Length == 2 &&
                double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                command.NearX = x;
                command.NearY = y;
                return;
            }
            throw new ActionProcessException($"Find: 'Near' must be a point ({{x, y}} or \"x,y\") or a token's id, not '{near}'.");
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Guid? OptionalGuid(string? value, string argument) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseGuid("Find", argument, value);

        private static double? OptionalNumber(string? value, string argument) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseNumber("Find", argument, value);
    }
}

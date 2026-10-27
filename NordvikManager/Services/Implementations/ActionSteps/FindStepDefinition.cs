using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Application.Exceptions;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionBody.Data;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    public class FindStepDefinition : IActionStepDefinition
    {
        public string Name => "Find";
        public string Value => "Find";
        public IReadOnlyList<string> Keywords => new[] { "tokens", "creatures", "monsters", "targets", "cards", "characters", "maps", "players", "search", "get", "list", "near", "in range" };
        public string Category => "Data";
        public string Description => "Finds the game's tokens, cards, maps, players… and stores them as a list (or the first one). " +
            "Tokens are looked for on the map the player is looking at unless you say otherwise.";
        public string? Summary => "Find {What}[ named {Name}][ with {Property}][ in a {Shape}][ near {Near}][ → {Output}]";
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
                Width = OptionalNumber(data.Width, "Width"),
                Height = OptionalNumber(data.Height, "Height"),
                Angle = OptionalNumber(data.Angle, "Angle"),
            };
            SetArea(command, data);

            if (OnMaps.Contains(what) && !data.AllMaps && command.Id == null)
                command.MapId = await ActiveMap.MapIdAsync(mediator, gameLobby, variables, "Find", data.MapId);

            // The battle map view "first" means the one the player is looking at.
            if (what == "battleMaps" && data.First && command.Id == null && command.Name == null)
                command.Id = (await ActiveMap.BattleMapAsync(mediator, gameLobby, variables, null))?.Id;

            var found = await mediator.Send(command) ?? new List<JObject>();
            // Nothing near a card: the card isn't on the map, so say so rather than find nothing.
            if (found.Count == 0)
            {
                if (command.NearElementId is Guid near) await MapPlace.ThrowIfCardAsync(mediator, gameLobby, "Find", "Near", near);
                if (command.TowardElementId is Guid toward) await MapPlace.ThrowIfCardAsync(mediator, gameLobby, "Find", "Toward", toward);
            }
            variables[data.Output] = data.First ? found.FirstOrDefault() : new JArray(found);
        }

        // Near (with Radius or Shape) makes an area; a line or cone also needs Toward. The same
        // arguments as the markers' (ShowMarker, AddElement, RequestPoint).
        private static void SetArea(FindCommand command, FindStepData data)
        {
            if (!string.IsNullOrWhiteSpace(data.Shape))
            {
                var shape = data.Shape.Trim().ToLowerInvariant();
                if (!AreaShape.Shapes.Contains(shape))
                    throw new ActionProcessException($"Find: 'Shape' must be {string.Join(", ", AreaShape.Shapes)}, not '{data.Shape}'.");
                if (string.IsNullOrWhiteSpace(data.Near))
                    throw new ActionProcessException("Find: an area needs 'Near': its centre, or where a line or cone starts." + MapPlace.EmptyHint);
                if ((shape == "line" || shape == "cone") && string.IsNullOrWhiteSpace(data.Toward))
                    throw new ActionProcessException($"Find: a {shape} needs 'Toward': the point or token it points at." + MapPlace.EmptyHint);
                command.Shape = shape;
            }

            if (!string.IsNullOrWhiteSpace(data.Near))
            {
                var near = MapPlace.Parse("Find", "Near", data.Near);
                (command.NearElementId, command.NearX, command.NearY) = (near.ElementId, near.X, near.Y);
            }
            if (!string.IsNullOrWhiteSpace(data.Toward))
            {
                var toward = MapPlace.Parse("Find", "Toward", data.Toward);
                (command.TowardElementId, command.TowardX, command.TowardY) = (toward.ElementId, toward.X, toward.Y);
            }
        }

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static Guid? OptionalGuid(string? value, string argument) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseGuid("Find", argument, value);

        private static double? OptionalNumber(string? value, string argument) =>
            string.IsNullOrWhiteSpace(value) ? null : ElementStepHelper.ParseNumber("Find", argument, value);
    }
}

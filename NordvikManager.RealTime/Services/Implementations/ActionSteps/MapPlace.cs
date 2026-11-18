using NordvikManager.Application.Commands.Actions.Find;
using NordvikManager.Application.Exceptions;
using MediatR;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace NordvikManager.Services.Implementations.ActionSteps
{
    /// <summary>
    /// A place on a map, as the steps take it (Find's Near and Toward, the markers' At and
    /// Toward, RequestPoint's From): a point ({"x":1,"y":2}, what %position% or %point% gives,
    /// or "x,y"), or a token or element — its id, or the object Find or RequestTargets gives
    /// (%target%), whose id is taken; or a list of one such (Request Targets with Max 1).
    /// </summary>
    public readonly record struct MapPlace(Guid? ElementId, double? X, double? Y)
    {
        /// <summary>
        /// Added to "needs a place" errors: tokens are filled in before the step runs, so a place
        /// written as a variable that has no value (e.g. %cardId% when no card ran the action)
        /// arrives empty.
        /// </summary>
        public const string EmptyHint = " It is empty here: if it holds a variable (e.g. %cardId%, set only when a card runs the action), that variable has no value.";

        public static MapPlace Parse(string step, string argument, string value)
        {
            var text = value.Trim();
            // A list of one (Request Targets with Max 1): that one.
            if (text.StartsWith("["))
            {
                try
                {
                    if (JArray.Parse(text) is { Count: 1 } one)
                        text = one[0].Type == JTokenType.Object ? one[0].ToString(Newtonsoft.Json.Formatting.None) : one[0].ToString();
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            if (Guid.TryParse(text, out var id))
                return new MapPlace(id, null, null);
            if (text.StartsWith("{"))
            {
                try
                {
                    var o = JObject.Parse(text);
                    if (Guid.TryParse((string?)o["id"], out var elementId))
                        return new MapPlace(elementId, null, null);
                    if (IsNumber(o["x"]) && IsNumber(o["y"]))
                        return new MapPlace(null, (double)o["x"]!, (double)o["y"]!);
                }
                catch (Newtonsoft.Json.JsonException) { }
            }
            var parts = text.Split(',');
            if (parts.Length == 2 &&
                double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                return new MapPlace(null, x, y);
            throw new ActionProcessException($"{step}: '{argument}' must be a point ({{x, y}} or \"x,y\") or a token (its id), not '{value}'.");
        }

        /// <summary>The place's point; for a token or element, its centre (and size).</summary>
        public async Task<(double X, double Y, double W, double H)> ResolveAsync(IMediator mediator, GameLobby gameLobby, string step, string argument)
        {
            if (ElementId is not Guid id)
                return (X!.Value, Y!.Value, 0, 0);
            var element = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "elements", Id = id }) ?? new List<JObject>()).FirstOrDefault();
            if (element == null)
            {
                await ThrowIfCardAsync(mediator, gameLobby, step, argument, id);
                throw new ActionProcessException($"{step}: '{argument}': there is no token or element '{id}' in this game.");
            }
            return ((double)element["x"]!, (double)element["y"]!, (double?)element["w"] ?? 0, (double?)element["h"] ?? 0);
        }

        /// <summary>
        /// A card's id (%cardId%) is the usual wrong place: the card isn't on the map, its token is.
        /// </summary>
        public static async Task ThrowIfCardAsync(IMediator mediator, GameLobby gameLobby, string step, string argument, Guid id)
        {
            var card = (await mediator.Send(new FindCommand { GameId = gameLobby.GameId, What = "cards", Id = id }) ?? new List<JObject>()).FirstOrDefault();
            if (card != null)
                throw new ActionProcessException($"{step}: '{argument}': '{id}' is a card ({card["name"]}), not a token on the map. " +
                    "Give its token: pick it with Request Targets, or use %elementId% from a token's right-click menu.");
        }

        private static bool IsNumber(JToken? value) => value?.Type is JTokenType.Integer or JTokenType.Float;
    }
}

using Newtonsoft.Json.Linq;

namespace DndOnePlaceManager.Application.Commands.Actions.Find
{
    /// <summary>
    /// Finds the game's things of one kind, for actions (the Find step and game.find in
    /// scripts). Each kind comes back in a fixed shape — see <see cref="FindShapes"/>.
    /// </summary>
    public class FindCommand : CommandBase<List<JObject>>
    {
        public Guid GameId { get; set; }

        /// <summary>tokens, elements, cards, maps, battleMaps, players, actions or layouts.</summary>
        public string What { get; set; } = "";

        public Guid? Id { get; set; }
        public string? Name { get; set; }

        /// <summary>Only things that have a property with this name (cards, maps, elements, tokens).</summary>
        public string? Property { get; set; }

        /// <summary>Tokens and elements: only this map's. Empty: every map of the game.</summary>
        public Guid? MapId { get; set; }

        /// <summary>Tokens: only those linked to a card.</summary>
        public bool WithCard { get; set; }

        /// <summary>Tokens and elements: only those within Radius squares of this point…</summary>
        public double? NearX { get; set; }
        public double? NearY { get; set; }

        /// <summary>…or of this element (which isn't included itself).</summary>
        public Guid? NearElementId { get; set; }

        /// <summary>In squares of the element's map; counted to the edge of a token, not its centre.</summary>
        public double? Radius { get; set; }

        /// <summary>Cards: templates are left out unless this is set.</summary>
        public bool IncludeTemplates { get; set; }
    }

    /// <summary>The fields each kind of Find result has, for the action editor and the docs.</summary>
    public static class FindShapes
    {
        public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["tokens"] = new[] { "id", "name", "x", "y", "w", "h", "cardId", "mapId" },
            ["elements"] = new[] { "id", "name", "type", "isToken", "x", "y", "w", "h", "cardId", "mapId", "object" },
            ["cards"] = new[] { "id", "name", "key", "isTemplate", "isHidden" },
            ["maps"] = new[] { "id", "name", "gridSize", "width", "height" },
            ["battleMaps"] = new[] { "id", "name", "mapId" },
            ["players"] = new[] { "id", "name", "color", "isOwner" },
            ["actions"] = new[] { "id", "name", "prefix", "hook" },
            ["layouts"] = new[] { "id", "name" },
        };
    }
}

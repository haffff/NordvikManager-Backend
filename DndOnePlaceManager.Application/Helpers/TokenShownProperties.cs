using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace DndOnePlaceManager.Application.Helpers
{
    /// <summary>
    /// The card properties a token displays, read from the definition stored with the
    /// token element (its "tokenData" and "tokenUiElements" details): every value rule
    /// (propDeps) with source "card", including the names referenced in an expression
    /// (%name%) or by a rule's propTargetMin / propTargetMax.
    /// </summary>
    public static class TokenShownProperties
    {
        private static readonly Regex ExpressionReference = new(@"%([^%\s]+)%", RegexOptions.Compiled);

        /// <summary>
        /// For each of these cards: the properties shown by the tokens placed for it, in
        /// this game, that the player can see (Read on the token element).
        /// </summary>
        public static Dictionary<Guid, HashSet<string>> ShownByVisibleTokens(IDbContext dbContext, IPermissionService permissionService, Guid gameId, Guid playerId, IReadOnlyCollection<Guid> cardIds)
        {
            var cardKeys = cardIds.Select(id => id.ToString()).ToList();
            var tokens = dbContext.Elements.AsNoTracking()
                .Where(e => e.Map!.Game.Id == gameId
                    && e.Details!.Any(d => d.Key == "cardId" && d.Value != null && cardKeys.Contains(d.Value)))
                .Select(e => new
                {
                    e.Id,
                    Details = e.Details!
                        .Where(d => d.Key == "cardId" || d.Key == "tokenData" || d.Key == "tokenUiElements")
                        .Select(d => new { d.Key, d.Value })
                        .ToList(),
                })
                .ToList();
            if (tokens.Count == 0)
                return new Dictionary<Guid, HashSet<string>>();

            var visible = permissionService.GetPermittedIds(playerId, tokens.Select(t => t.Id), Domain.Enums.Permission.Read);

            var shown = new Dictionary<Guid, HashSet<string>>();
            foreach (var token in tokens.Where(t => visible.Contains(t.Id)))
            {
                string? Detail(string key) => token.Details.FirstOrDefault(d => d.Key == key)?.Value;
                if (!Guid.TryParse(Detail("cardId"), out var cardId))
                    continue;

                var names = CardProperties(Detail("tokenData"), Detail("tokenUiElements"));
                if (!shown.TryGetValue(cardId, out var forCard))
                    shown[cardId] = forCard = new HashSet<string>(StringComparer.Ordinal);
                forCard.UnionWith(names);
            }
            return shown;
        }

        public static HashSet<string> CardProperties(string? tokenDataJson, string? additionsJson)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            if (Parse(tokenDataJson) is JObject tokenData)
                AddFromDeps(tokenData["propDeps"], names);

            if (Parse(additionsJson) is JArray additions)
            {
                foreach (var addition in additions.OfType<JObject>())
                    AddFromDeps(addition["tokenData"]?["propDeps"], names);
            }

            return names;
        }

        private static JToken? Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                return JToken.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void AddFromDeps(JToken? deps, HashSet<string> names)
        {
            if (deps is not JArray array)
                return;

            foreach (var dep in array.OfType<JObject>())
            {
                if (!string.Equals(dep.Value<string>("source"), "card", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (dep.Value<string>("dtoProperty") is { Length: > 0 } property)
                    names.Add(property);

                if (dep.Value<string>("expression") is { Length: > 0 } expression)
                {
                    foreach (Match match in ExpressionReference.Matches(expression))
                        names.Add(match.Groups[1].Value);
                }

                if (dep["rule"]?["arguments"] is JObject arguments)
                {
                    foreach (var key in new[] { "propTargetMin", "propTargetMax" })
                    {
                        if (arguments[key]?.Type == JTokenType.String && arguments.Value<string>(key) is { Length: > 0 } referenced)
                            names.Add(referenced);
                    }
                }
            }
        }
    }
}

using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.Services.Implementations
{
    /// <summary>
    /// Resolves %q:{varOrGuid}.propName%, %qn:type-"name".propName%, and %v:varName.field%
    /// patterns before the standard %varName% pass in Prepare().
    /// </summary>
    public class ActionPropertyQueryResolver
    {
        private static readonly Regex _qPattern =
            new Regex(@"\%q:([^.%]+)\.([^%]+)\%", RegexOptions.Compiled);

        private static readonly Regex _qnPattern =
            new Regex(@"\%qn:(\w+)-""([^""]+)""\.([^%]+)\%", RegexOptions.Compiled);

        // %v:varName.fieldName% — reads a public property from a complex object stored in variables
        private static readonly Regex _vPattern =
            new Regex(@"\%v:(\w+)\.(\w+)\%", RegexOptions.Compiled);

        private readonly IDbContext _db;
        private readonly Guid _gameId;

        public ActionPropertyQueryResolver(IDbContext db, Guid gameId)
        {
            _db = db;
            _gameId = gameId;
        }

        /// <summary>
        /// Replaces all %q:...% and %qn:...% tokens in <paramref name="raw"/> with resolved values.
        /// Variable references inside the specifier (e.g. %q:{gameId}.name%) are resolved first
        /// using <paramref name="variables"/>.
        /// </summary>
        public async Task<string> PreResolveQueriesAsync(string raw, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            // Handle %v:varName.field% — reflect into a complex object stored in variables
            foreach (Match m in _vPattern.Matches(raw))
            {
                var varName   = m.Groups[1].Value;
                var fieldName = m.Groups[2].Value;
                string resolved = string.Empty;

                if (variables.TryGetValue(varName, out var obj) && obj != null)
                {
                    var prop = obj.GetType().GetProperty(fieldName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    resolved = prop?.GetValue(obj)?.ToString() ?? string.Empty;
                }

                raw = raw.Replace(m.Value, resolved);
            }

            // Handle %qn:type-"name".prop%
            var qnMatches = _qnPattern.Matches(raw);
            foreach (Match m in qnMatches)
            {
                var entityType = m.Groups[1].Value;
                var entityName = m.Groups[2].Value;
                var propName   = m.Groups[3].Value;

                var resolved = await ResolveByNameAsync(entityType, entityName, propName);
                raw = raw.Replace(m.Value, resolved ?? string.Empty);
            }

            // Handle %q:{varName}.prop% and %q:guid.prop%
            var qMatches = _qPattern.Matches(raw);
            foreach (Match m in qMatches)
            {
                var specifier = m.Groups[1].Value; // e.g. "{gameId}" or "some-guid"
                var propName  = m.Groups[2].Value;

                // Resolve {varName} references inside the specifier
                var guidStr = ResolveVarRef(specifier, variables);

                var resolved = await ResolveByIdAsync(guidStr, propName);
                raw = raw.Replace(m.Value, resolved ?? string.Empty);
            }

            return raw;
        }

        /// <summary>Resolves a single {varName} reference, returning the plain string otherwise.</summary>
        private static string ResolveVarRef(string specifier, Dictionary<string, object> variables)
        {
            if (specifier.StartsWith("{") && specifier.EndsWith("}"))
            {
                var varName = specifier[1..^1];
                if (variables.TryGetValue(varName, out var val))
                    return val?.ToString() ?? string.Empty;
            }
            return specifier;
        }

        /// <summary>
        /// Resolves a property for an entity identified by GUID string.
        /// Property resolution order:
        ///   1. "id"   → return the GUID itself
        ///   2. "name" → look up .Name on known named tables
        ///   3. other  → query Properties table
        /// </summary>
        public async Task<string?> ResolveByIdAsync(string guidStr, string propName)
        {
            if (!Guid.TryParse(guidStr, out var guid))
                return null;

            if (string.Equals(propName, "id", StringComparison.OrdinalIgnoreCase))
                return guid.ToString();

            if (string.Equals(propName, "name", StringComparison.OrdinalIgnoreCase))
                return await ResolveNameByIdAsync(guid);

            var prop = await _db.Properties
                .FirstOrDefaultAsync(p =>
                    p.ParentID == guid &&
                    p.Name == propName &&
                    !p.IsProtected &&
                    (p.Game.Id == _gameId ||
                     p.Map.Game.Id == _gameId ||
                     p.Element.Map.Game.Id == _gameId ||
                     p.Card.GameId == _gameId));
            return prop?.Value;
        }

        /// <summary>
        /// Resolves a property for an entity identified by type + name.
        /// </summary>
        public async Task<string?> ResolveByNameAsync(string entityType, string entityName, string propName)
        {
            var guid = await FindGuidByTypeAndNameAsync(entityType, entityName);
            if (guid == null)
                return null;

            return await ResolveByIdAsync(guid.Value.ToString(), propName);
        }

        private async Task<string?> ResolveNameByIdAsync(Guid guid)
        {
            var game = await _db.Games.FirstOrDefaultAsync(g => g.Id == guid && g.Id == _gameId);
            if (game != null) return game.Name;

            var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == guid && c.GameId == _gameId);
            if (card != null) return card.Name;

            var player = await _db.Players.FirstOrDefaultAsync(p => p.Id == guid && p.Game.Id == _gameId);
            if (player != null) return player.Name;

            var action = await _db.Actions.FirstOrDefaultAsync(a => a.Id == guid && a.Game.Id == _gameId);
            if (action != null) return action.Name;

            var map = await _db.Maps.FirstOrDefaultAsync(m => m.Id == guid && m.Game.Id == _gameId);
            if (map != null) return map.Name;

            return null;
        }

        private async Task<Guid?> FindGuidByTypeAndNameAsync(string entityType, string entityName)
        {
            switch (entityType.ToLowerInvariant())
            {
                case "game":
                    var g = await _db.Games.FirstOrDefaultAsync(x => x.Name == entityName && x.Id == _gameId);
                    return g?.Id;
                case "card":
                    var c = await _db.Cards.FirstOrDefaultAsync(x => x.Name == entityName && x.GameId == _gameId);
                    return c?.Id;
                case "player":
                    var p = await _db.Players.FirstOrDefaultAsync(x => x.Name == entityName && x.Game.Id == _gameId);
                    return p?.Id;
                case "action":
                    if (entityName.Contains('/'))
                    {
                        var slash = entityName.IndexOf('/');
                        var pfx  = entityName[..slash];
                        var nm   = entityName[(slash + 1)..];
                        var a = await _db.Actions.FirstOrDefaultAsync(x => x.Prefix == pfx && x.Name == nm && x.Game.Id == _gameId);
                        return a?.Id;
                    }
                    else
                    {
                        var a = await _db.Actions.FirstOrDefaultAsync(x => x.Name == entityName && x.Game.Id == _gameId);
                        return a?.Id;
                    }
                case "map":
                    var m = await _db.Maps.FirstOrDefaultAsync(x => x.Name == entityName && x.Game.Id == _gameId);
                    return m?.Id;
                default:
                    return null;
            }
        }
    }
}

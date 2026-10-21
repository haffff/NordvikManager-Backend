using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Services.Actions
{
    /// <summary>
    /// Fills in the tokens in an action step argument, in one pass — what a token turns
    /// into is never read as tokens again, so text a player typed can't smuggle a query in:
    /// <list type="bullet">
    /// <item><c>%name%</c> a variable as text (an unknown one is left as it is)</item>
    /// <item><c>%dto:name%</c> a variable as JSON</item>
    /// <item><c>%v:name.path[0].x%</c> a value inside an object variable</item>
    /// <item><c>%q:type[name].prop%</c> a property of the game's entity with that name
    /// (type: game, card, player, action, map, resource)</item>
    /// <item><c>%q:{var}.prop%</c> / <c>%q:&lt;guid&gt;.prop%</c> a property of the entity with that id</item>
    /// </list>
    /// <c>.id</c> and <c>.name</c> give the entity's own id and name. Queries only see the
    /// game's own entities and never protected properties; a miss gives empty text and is
    /// listed in <see cref="Unresolved"/>.
    /// </summary>
    public class ActionTokenResolver
    {
        private const string Guid = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

        private static readonly Regex _tokenPattern = new(
            @"%(?:" +
            @"q:(?<type>[A-Za-z]+)\[(?<name>[^\]]+)\]\.(?<prop>[^%]+)" +
            @"|q:\{(?<var>\w+)\}\.(?<prop>[^%]+)" +
            @"|q:(?<guid>" + Guid + @")\.(?<prop>[^%]+)" +
            @"|v:(?<root>\w+)(?<path>(?:\.\w+|\[\d+\])+)" +
            @"|(?<dto>dto:)?(?<word>\w+)" +
            @")%",
            RegexOptions.Compiled);

        private static readonly JsonSerializer _dtoSerializer = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        private readonly IDbContext _db;
        private readonly System.Guid _gameId;
        private readonly List<string> _unresolved = new();

        public ActionTokenResolver(IDbContext db, System.Guid gameId)
        {
            _db = db;
            _gameId = gameId;
        }

        /// <summary>Tokens that came out empty because what they point at wasn't found.</summary>
        public IReadOnlyList<string> Unresolved => _unresolved;

        /// <summary>Fills in every token in <paramref name="raw"/>, queries included.</summary>
        public async Task<string> ResolveAsync(string raw, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(raw) || !raw.Contains('%'))
                return raw;

            var output = new StringBuilder();
            var last = 0;
            foreach (Match m in _tokenPattern.Matches(raw))
            {
                output.Append(raw, last, m.Index - last);
                output.Append(m.Groups["prop"].Success
                    ? await ResolveQueryAsync(m, variables)
                    : ResolveVariable(m, variables, _unresolved));
                last = m.Index + m.Length;
            }
            output.Append(raw, last, raw.Length - last);
            return output.ToString();
        }

        /// <summary>
        /// Fills in variable tokens only (%name%, %dto:name%, %v:path%), leaving %q:% queries
        /// as they are — for places with no database at hand.
        /// </summary>
        public static string ResolveVariables(string raw, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(raw) || !raw.Contains('%'))
                return raw;

            return _tokenPattern.Replace(raw, m =>
                m.Groups["prop"].Success ? m.Value : ResolveVariable(m, variables, null));
        }

        private static string ResolveVariable(Match m, Dictionary<string, object> variables, List<string>? unresolved)
        {
            if (m.Groups["root"].Success)
            {
                var value = variables.TryGetValue(m.Groups["root"].Value, out var root)
                    ? VariablePathResolver.Resolve(root, m.Groups["path"].Value)
                    : null;
                if (value == null)
                    unresolved?.Add(m.Value);
                return VariablePathResolver.Stringify(value);
            }

            if (!variables.TryGetValue(m.Groups["word"].Value, out var variable))
                return m.Value;

            if (m.Groups["dto"].Success)
                return variable == null ? "null" : JToken.FromObject(variable, _dtoSerializer).ToString(Formatting.None);

            return variable?.ToString() ?? string.Empty;
        }

        private async Task<string> ResolveQueryAsync(Match m, Dictionary<string, object> variables)
        {
            System.Guid? id;
            if (m.Groups["type"].Success)
                id = await FindByNameAsync(m.Groups["type"].Value, m.Groups["name"].Value);
            else
            {
                var idText = m.Groups["var"].Success
                    ? (variables.TryGetValue(m.Groups["var"].Value, out var v) ? v?.ToString() : null)
                    : m.Groups["guid"].Value;
                id = System.Guid.TryParse(idText, out var parsed) ? parsed : null;
            }

            var resolved = id == null ? null : await ReadAsync(id.Value, m.Groups["prop"].Value);
            if (resolved == null)
                _unresolved.Add(m.Value);
            return resolved ?? string.Empty;
        }

        // ── Reading ──────────────────────────────────────────────────────────────

        /// <summary>id → the id, name → the entity's name, else its (unprotected) property; null when not this game's.</summary>
        private async Task<string?> ReadAsync(System.Guid id, string propName)
        {
            if (string.Equals(propName, "id", StringComparison.OrdinalIgnoreCase))
                return await IsInGameAsync(id) ? id.ToString() : null;

            if (string.Equals(propName, "name", StringComparison.OrdinalIgnoreCase))
                return await NameOfAsync(id);

            return await _db.Properties
                .Where(p =>
                    p.ParentID == id &&
                    p.Name == propName &&
                    !p.IsProtected &&
                    (p.Game.Id == _gameId ||
                     p.Map.Game.Id == _gameId ||
                     p.Element.Map.Game.Id == _gameId ||
                     p.Card.GameId == _gameId))
                .Select(p => p.Value)
                .FirstOrDefaultAsync();
        }

        private async Task<string?> NameOfAsync(System.Guid id) =>
            await _db.Games.Where(x => x.Id == id && x.Id == _gameId).Select(x => x.Name).FirstOrDefaultAsync()
            ?? await _db.Cards.Where(x => x.Id == id && x.GameId == _gameId).Select(x => x.Name).FirstOrDefaultAsync()
            ?? await _db.Players.Where(x => x.Id == id && x.Game.Id == _gameId).Select(x => x.Name).FirstOrDefaultAsync()
            ?? await _db.Actions.Where(x => x.Id == id && x.Game.Id == _gameId).Select(x => x.Name).FirstOrDefaultAsync()
            ?? await _db.Maps.Where(x => x.Id == id && x.Game.Id == _gameId).Select(x => x.Name).FirstOrDefaultAsync()
            ?? await _db.Resources.Where(x => x.Id == id && x.GameId == _gameId).Select(x => x.Name).FirstOrDefaultAsync();

        private async Task<bool> IsInGameAsync(System.Guid id) =>
            id == _gameId
            || await _db.Cards.AnyAsync(x => x.Id == id && x.GameId == _gameId)
            || await _db.Players.AnyAsync(x => x.Id == id && x.Game.Id == _gameId)
            || await _db.Actions.AnyAsync(x => x.Id == id && x.Game.Id == _gameId)
            || await _db.Maps.AnyAsync(x => x.Id == id && x.Game.Id == _gameId)
            || await _db.Elements.AnyAsync(x => x.Id == id && x.Map!.Game.Id == _gameId)
            || await _db.Resources.AnyAsync(x => x.Id == id && x.GameId == _gameId);

        private async Task<System.Guid?> FindByNameAsync(string type, string name)
        {
            switch (type.ToLowerInvariant())
            {
                case "game":
                    return await _db.Games.Where(x => x.Name == name && x.Id == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                case "card":
                    return await _db.Cards.Where(x => x.Name == name && x.GameId == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                case "player":
                    return await _db.Players.Where(x => x.Name == name && x.Game.Id == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                case "map":
                    return await _db.Maps.Where(x => x.Name == name && x.Game.Id == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                case "action":
                    // "prefix/name" picks one addon's action; a bare name matches any prefix.
                    var slash = name.IndexOf('/');
                    if (slash >= 0)
                    {
                        var prefix = name[..slash];
                        var actionName = name[(slash + 1)..];
                        return await _db.Actions.Where(x => x.Prefix == prefix && x.Name == actionName && x.Game.Id == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                    }
                    return await _db.Actions.Where(x => x.Name == name && x.Game.Id == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                case "resource":
                    // By key first (an addon's resources are keyed "<addon>_<file>"), then by
                    // name. Only the id is read, never the file bytes.
                    return await _db.Resources.Where(x => x.Key == name && x.GameId == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync()
                        ?? await _db.Resources.Where(x => x.Name == name && x.GameId == _gameId).Select(x => (System.Guid?)x.Id).FirstOrDefaultAsync();
                default:
                    return null;
            }
        }
    }
}

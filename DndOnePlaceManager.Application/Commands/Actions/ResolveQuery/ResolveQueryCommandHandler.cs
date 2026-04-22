using AutoMapper;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DndOnePlaceManager.Application.Commands.Actions.ResolveQuery
{
    internal class ResolveQueryCommandHandler : HandlerBase<ResolveQueryCommand, string>
    {
        private static readonly Regex _qPattern   = new(@"\%q:([^.%]+)\.([^%]+)\%",            RegexOptions.Compiled);
        private static readonly Regex _qnPattern  = new(@"\%qn:(\w+)-""([^""]+)""\.([^%]+)\%", RegexOptions.Compiled);
        private static readonly Regex _vPattern   = new(@"\%v:(\w+)\.(\w+)\%",                 RegexOptions.Compiled);
        private static readonly Regex _varPattern = new(@"\%(\w+)\%",                           RegexOptions.Compiled);

        public ResolveQueryCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper) { }

        public override async Task<string> Handle(ResolveQueryCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            // Build an allowlist of entity IDs that belong to this game.
            // Any GUID not in this set is rejected — prevents cross-game property scraping.
            var allowedIds = await BuildAllowedIdsAsync(request.GameId);

            var vars = request.Variables ?? new Dictionary<string, object>();
            vars["gameId"]  = request.GameId.ToString();
            if (request.Player?.Id.HasValue == true)
                vars["playerId"] = request.Player.Id.Value.ToString();

            var result = request.Expression;

            // %v:varName.field% — reflect into objects the caller explicitly supplied
            foreach (Match m in _vPattern.Matches(result))
            {
                var varName   = m.Groups[1].Value;
                var fieldName = m.Groups[2].Value;
                string resolved = string.Empty;
                if (vars.TryGetValue(varName, out var obj) && obj != null)
                {
                    var prop = obj.GetType().GetProperty(fieldName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    resolved = prop?.GetValue(obj)?.ToString() ?? string.Empty;
                }
                result = result.Replace(m.Value, resolved);
            }

            // %qn:type-"name".prop%
            foreach (Match m in _qnPattern.Matches(result))
            {
                var entityType = m.Groups[1].Value;
                var entityName = m.Groups[2].Value;
                var propName   = m.Groups[3].Value;
                var resolved   = await ResolveByNameAsync(request.GameId, entityType, entityName, propName, allowedIds);
                result = result.Replace(m.Value, resolved ?? string.Empty);
            }

            // %q:{varName}.prop% or %q:guid.prop%
            foreach (Match m in _qPattern.Matches(result))
            {
                var specifier = m.Groups[1].Value;
                var propName  = m.Groups[2].Value;
                var guidStr   = ResolveVarRef(specifier, vars);
                var resolved  = await ResolveByIdAsync(guidStr, propName, allowedIds);
                result = result.Replace(m.Value, resolved ?? string.Empty);
            }

            // %varName%
            result = _varPattern.Replace(result, m =>
            {
                var key = m.Groups[1].Value;
                return vars.TryGetValue(key, out var val) ? val?.ToString() ?? string.Empty : m.Value;
            });

            return result;
        }

        // ── Allowlist ────────────────────────────────────────────────────────────

        private async Task<HashSet<Guid>> BuildAllowedIdsAsync(Guid gameId)
        {
            var game = await dbContext.Games
                .Include(g => g.Maps)
                .Include(g => g.Cards)
                .Include(g => g.Players)
                .Include(g => g.Actions)
                .FirstOrDefaultAsync(g => g.Id == gameId);

            var ids = new HashSet<Guid>();
            if (game == null) return ids;

            ids.Add(game.Id);
            game.Maps?.ForEach(e => ids.Add(e.Id));
            game.Cards?.ForEach(e => ids.Add(e.Id));
            game.Players?.ForEach(e => ids.Add(e.Id));
            game.Actions?.ForEach(e => ids.Add(e.Id));
            return ids;
        }

        // ── Resolution ───────────────────────────────────────────────────────────

        private static string ResolveVarRef(string specifier, Dictionary<string, object> vars)
        {
            if (specifier.StartsWith("{") && specifier.EndsWith("}"))
            {
                var key = specifier[1..^1];
                if (vars.TryGetValue(key, out var val))
                    return val?.ToString() ?? string.Empty;
            }
            return specifier;
        }

        private async Task<string?> ResolveByIdAsync(string guidStr, string propName, HashSet<Guid> allowedIds)
        {
            if (!Guid.TryParse(guidStr, out var guid))
                return null;

            if (!allowedIds.Contains(guid))
                return null;

            if (string.Equals(propName, "id", StringComparison.OrdinalIgnoreCase))
                return guid.ToString();

            if (string.Equals(propName, "name", StringComparison.OrdinalIgnoreCase))
                return await ResolveNameByIdAsync(guid);

            // Never return protected properties
            var prop = await dbContext.Properties
                .FirstOrDefaultAsync(p => p.ParentID == guid && p.Name == propName && p.IsProtected == false);
            return prop?.Value;
        }

        private async Task<string?> ResolveByNameAsync(Guid gameId, string entityType, string entityName, string propName, HashSet<Guid> allowedIds)
        {
            var guid = await FindGuidByTypeAndNameAsync(gameId, entityType, entityName);
            if (guid == null || !allowedIds.Contains(guid.Value))
                return null;
            return await ResolveByIdAsync(guid.Value.ToString(), propName, allowedIds);
        }

        private async Task<string?> ResolveNameByIdAsync(Guid guid)
        {
            var game = await dbContext.Games.FindAsync(guid);
            if (game != null) return game.Name;
            var card = await dbContext.Cards.FindAsync(guid);
            if (card != null) return card.Name;
            var player = await dbContext.Players.FindAsync(guid);
            if (player != null) return player.Name;
            var action = await dbContext.Actions.FindAsync(guid);
            if (action != null) return action.Name;
            var map = await dbContext.Maps.FindAsync(guid);
            if (map != null) return map.Name;
            return null;
        }

        private async Task<Guid?> FindGuidByTypeAndNameAsync(Guid gameId, string entityType, string entityName)
        {
            switch (entityType.ToLowerInvariant())
            {
                case "game":
                    var g = await dbContext.Games.FirstOrDefaultAsync(x => x.Id == gameId && x.Name == entityName);
                    return g?.Id;
                case "card":
                    var c = await dbContext.Cards.FirstOrDefaultAsync(x => x.GameId == gameId && x.Name == entityName);
                    return c?.Id;
                case "player":
                    var p = await dbContext.Players.FirstOrDefaultAsync(x => x.Game.Id == gameId && x.Name == entityName);
                    return p?.Id;
                case "action":
                    if (entityName.Contains('/'))
                    {
                        var slash = entityName.IndexOf('/');
                        var pfx  = entityName[..slash];
                        var nm   = entityName[(slash + 1)..];
                        var a = await dbContext.Actions.FirstOrDefaultAsync(x => x.Game.Id == gameId && x.Prefix == pfx && x.Name == nm);
                        return a?.Id;
                    }
                    else
                    {
                        var a = await dbContext.Actions.FirstOrDefaultAsync(x => x.Game.Id == gameId && x.Name == entityName);
                        return a?.Id;
                    }
                case "map":
                    var m = await dbContext.Maps.FirstOrDefaultAsync(x => x.Game.Id == gameId && x.Name == entityName);
                    return m?.Id;
                default:
                    return null;
            }
        }
    }
}

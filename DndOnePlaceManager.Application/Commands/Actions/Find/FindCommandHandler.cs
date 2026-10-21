using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Mapping;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace DndOnePlaceManager.Application.Commands.Actions.Find
{
    internal class FindCommandHandler : HandlerBase<FindCommand, List<JObject>>
    {
        public FindCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<List<JObject>> Handle(FindCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);
            var gameId = request.GameId;
            var withProperty = string.IsNullOrWhiteSpace(request.Property) ? null : OwnersOf(request.Property);

            switch (request.What.Trim().ToLowerInvariant())
            {
                case "tokens":
                    return Elements(request, withProperty, tokensOnly: true);
                case "elements":
                    return Elements(request, withProperty, tokensOnly: false);
                case "cards":
                    var cards = dbContext.Cards.Where(c => c.GameId == gameId && !c.IsCustomUi);
                    if (!request.IncludeTemplates && request.Id == null)
                        cards = cards.Where(c => !c.IsTemplate);
                    cards = Filter(cards, request, withProperty, c => c.Id, c => c.Name);
                    return cards.Select(c => new { c.Id, c.Name, c.Key, c.IsTemplate, c.IsHidden }).AsEnumerable()
                        .Select(c => Shape(("id", c.Id), ("name", c.Name), ("key", c.Key), ("isTemplate", c.IsTemplate), ("isHidden", c.IsHidden)))
                        .ToList();
                case "maps":
                    return Filter(dbContext.Maps.Where(m => m.Game.Id == gameId), request, withProperty, m => m.Id, m => m.Name)
                        .Select(m => new { m.Id, m.Name, m.GridSize, m.Width, m.Height }).AsEnumerable()
                        .Select(m => Shape(("id", m.Id), ("name", m.Name), ("gridSize", m.GridSize), ("width", m.Width), ("height", m.Height)))
                        .ToList();
                case "battlemaps":
                    return Filter(dbContext.BattleMaps.Where(b => b.Game.Id == gameId), request, null, b => b.Id, b => b.Name)
                        .Select(b => new { b.Id, b.Name, b.MapId }).AsEnumerable()
                        .Select(b => Shape(("id", b.Id), ("name", b.Name), ("mapId", b.MapId)))
                        .ToList();
                case "players":
                    var masterId = dbContext.Games.Where(g => g.Id == gameId).Select(g => g.MasterId).FirstOrDefault();
                    return Filter(dbContext.Players.Where(p => p.Game.Id == gameId && !p.System), request, null, p => p.Id, p => p.Name)
                        .Select(p => new { p.Id, p.Name, p.Color }).AsEnumerable()
                        .Select(p => Shape(("id", p.Id), ("name", p.Name), ("color", p.Color), ("isOwner", p.Id == masterId)))
                        .ToList();
                case "actions":
                    return Filter(dbContext.Actions.Where(a => a.Game.Id == gameId), request, null, a => a.Id, a => a.Name)
                        .Select(a => new { a.Id, a.Name, a.Prefix, a.Hook }).AsEnumerable()
                        .Select(a => Shape(("id", a.Id), ("name", a.Name), ("prefix", a.Prefix), ("hook", (int)a.Hook)))
                        .ToList();
                case "layouts":
                    return Filter(dbContext.Layouts.Where(l => l.GameModelId == gameId), request, null, l => l.Id, l => l.Name)
                        .Select(l => new { l.Id, l.Name }).AsEnumerable()
                        .Select(l => Shape(("id", l.Id), ("name", l.Name)))
                        .ToList();
                default:
                    throw new ArgumentException($"Find: unknown kind '{request.What}'. Use one of: {string.Join(", ", FindShapes.Fields.Keys)}.");
            }
        }

        // Ids of whatever has a property with this name.
        private IQueryable<Guid> OwnersOf(string property) =>
            dbContext.Properties.Where(p => p.Name == property).Select(p => p.ParentID);

        private static IQueryable<T> Filter<T>(IQueryable<T> query, FindCommand request, IQueryable<Guid>? withProperty,
            System.Linq.Expressions.Expression<Func<T, Guid>> id, System.Linq.Expressions.Expression<Func<T, string?>> name)
        {
            if (request.Id is Guid wanted)
                query = query.Where(Equals(id, wanted));
            if (!string.IsNullOrWhiteSpace(request.Name))
                query = query.Where(Equals(name, request.Name));
            if (withProperty != null)
            {
                var parameter = id.Parameters[0];
                var contains = System.Linq.Expressions.Expression.Call(typeof(Queryable), nameof(Queryable.Contains), new[] { typeof(Guid) },
                    withProperty.Expression, id.Body);
                query = query.Where(System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(contains, parameter));
            }
            return query;
        }

        private static System.Linq.Expressions.Expression<Func<T, bool>> Equals<T, TValue>(System.Linq.Expressions.Expression<Func<T, TValue>> member, TValue value) =>
            System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
                System.Linq.Expressions.Expression.Equal(member.Body, System.Linq.Expressions.Expression.Constant(value, typeof(TValue))),
                member.Parameters);

        // ── Tokens and elements ─────────────────────────────────────────────────

        private List<JObject> Elements(FindCommand request, IQueryable<Guid>? withProperty, bool tokensOnly)
        {
            var query = dbContext.Elements.Include(e => e.Details).Include(e => e.Map)
                .Where(e => e.Map!.Game.Id == request.GameId);
            if (request.MapId is Guid mapId)
                query = query.Where(e => e.MapId == mapId);
            if (request.Id is Guid id)
                query = query.Where(e => e.Id == id);
            if (withProperty != null)
                query = query.Where(e => withProperty.Contains(e.Id));

            var found = query.AsEnumerable().Select(e => Describe(e)).Where(x => x != null).Select(x => x!.Value).ToList();

            if (tokensOnly)
                found = found.Where(x => x.IsToken).ToList();
            if (request.WithCard)
                found = found.Where(x => x.CardId != null).ToList();
            if (!string.IsNullOrWhiteSpace(request.Name))
                found = found.Where(x => x.Name == request.Name).ToList();

            if (request.Radius is double radius && (request.NearElementId != null || (request.NearX != null && request.NearY != null)))
            {
                double cx, cy;
                if (request.NearElementId is Guid near)
                {
                    var centre = dbContext.Elements.Include(e => e.Details).Include(e => e.Map)
                        .Where(e => e.Id == near && e.Map!.Game.Id == request.GameId).AsEnumerable()
                        .Select(e => Describe(e)).FirstOrDefault();
                    if (centre == null)
                        return new List<JObject>();
                    (cx, cy) = (centre.Value.X, centre.Value.Y);
                    found = found.Where(x => x.Id != near).ToList();
                }
                else
                    (cx, cy) = (request.NearX!.Value, request.NearY!.Value);

                found = found.Where(x =>
                {
                    var dx = x.X - cx;
                    var dy = x.Y - cy;
                    return Math.Sqrt(dx * dx + dy * dy) <= radius * x.GridSize + Math.Min(x.W, x.H) / 2;
                }).ToList();
            }

            return found.Select(x => tokensOnly
                    ? Shape(("id", x.Id), ("name", x.Name), ("x", x.X), ("y", x.Y), ("w", x.W), ("h", x.H), ("cardId", x.CardId), ("mapId", x.MapId))
                    : Shape(("id", x.Id), ("name", x.Name), ("type", x.Type), ("isToken", x.IsToken), ("x", x.X), ("y", x.Y), ("w", x.W), ("h", x.H),
                        ("cardId", x.CardId), ("mapId", x.MapId), ("object", x.Object)))
                .ToList();
        }

        private readonly record struct Described(Guid Id, Guid MapId, int GridSize, string? Name, string? Type, bool IsToken,
            double X, double Y, double W, double H, string? CardId, JObject Object);

        // The element's canvas object, its size on the map and its centre.
        private Described? Describe(ElementModel element)
        {
            var json = mapper.Map<ElementDTO>(element)?.Object;
            if (string.IsNullOrEmpty(json))
                return null;
            JObject o;
            try { o = JObject.Parse(json); }
            catch (Newtonsoft.Json.JsonException) { return null; }

            double Num(string key, double fallback) => o[key]?.Type is JTokenType.Integer or JTokenType.Float ? (double)o[key]! : fallback;
            var w = Num("width", 0) * Num("scaleX", 1);
            var h = Num("height", 0) * Num("scaleY", 1);
            var left = Num("left", 0);
            var top = Num("top", 0);
            var x = (string?)o["originX"] == "center" ? left : left + w / 2;
            var y = (string?)o["originY"] == "center" ? top : top + h / 2;
            var cardId = (string?)o["tokenData"]?["cardId"] ?? (string?)o["cardId"];

            return new Described(element.Id, element.MapId, element.Map?.GridSize is > 0 ? element.Map.GridSize : 50,
                (string?)o["name"], (string?)o["type"], o["isToken"]?.Type == JTokenType.Boolean && (bool)o["isToken"]!,
                x, y, w, h, string.IsNullOrEmpty(cardId) ? null : cardId, o);
        }

        private static JObject Shape(params (string Name, object? Value)[] fields)
        {
            var shape = new JObject();
            foreach (var (name, value) in fields)
                shape[name] = value switch
                {
                    null => JValue.CreateNull(),
                    Guid g => g.ToString(),
                    JToken t => t,
                    _ => JToken.FromObject(value),
                };
            return shape;
        }
    }
}

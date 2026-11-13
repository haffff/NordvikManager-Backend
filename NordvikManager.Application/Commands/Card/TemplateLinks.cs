using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Infrastructure.Interfaces;

namespace NordvikManager.Application.Commands.Card
{
    /// <summary>
    /// A card made from a template points at it by its "template_id" property (the template's
    /// id) and remembers the template's key in "template_key". Ids change when a template is
    /// re-created (an addon update removes and re-adds its templates) or the card goes to
    /// another game; the key doesn't, so the link is found again by it. A template without a
    /// key (the GM sets one in its settings) links only by id.
    /// </summary>
    internal static class TemplateLinks
    {
        public const string IdProperty = "template_id";
        public const string KeyProperty = "template_key";

        /// <summary>
        /// Cards of the template's game that remember its key and whose template is gone
        /// (or never was) now point at it. Saves; returns how many.
        /// </summary>
        public static int LinkOrphans(IDbContext db, CardModel template)
        {
            if (!template.IsTemplate || string.IsNullOrWhiteSpace(template.Key))
                return 0;

            var gameId = template.GameId;
            var cardIds = CardsOf(db, gameId, db.Properties
                .Where(p => p.Name == KeyProperty && p.Value == template.Key)
                .Select(p => p.ParentID)
                .ToList());
            if (cardIds.Count == 0)
                return 0;

            var templates = TemplateIds(db, gameId);
            var links = db.Properties.Where(p => p.Name == IdProperty && cardIds.Contains(p.ParentID)).ToList();
            var linked = 0;
            foreach (var cardId in cardIds)
            {
                var link = links.FirstOrDefault(p => p.ParentID == cardId);
                if (link != null && Guid.TryParse(link.Value, out var current) && templates.Contains(current))
                    continue; // its template is still here
                Set(db, cardId, IdProperty, template.Id.ToString(), link);
                linked++;
            }
            db.SaveChanges();
            return linked;
        }

        /// <summary>
        /// The template's key changed: the cards made from it remember the new one (and
        /// orphans with the new key link to it). Saves.
        /// </summary>
        public static void KeyChanged(IDbContext db, CardModel template)
        {
            RememberKey(db, template.GameId, new[] { template.Id }, _ => template.Key);
            LinkOrphans(db, template);
        }

        /// <summary>
        /// Cards made from these templates remember their keys (cards made before keys were
        /// kept don't): done before the templates are removed and re-created. Saves.
        /// </summary>
        public static void RememberKeys(IDbContext db, Guid gameId, IEnumerable<CardModel> templates)
        {
            var keys = templates.Where(t => !string.IsNullOrWhiteSpace(t.Key)).ToDictionary(t => t.Id, t => t.Key);
            RememberKey(db, gameId, keys.Keys, id => keys[id]);
        }

        /// <summary>
        /// An addon's template whose key is "&lt;addon&gt;_&lt;key&gt;": cards whose template is gone
        /// and that remember the bare "&lt;key&gt;" (addon template keys weren't prefixed at first)
        /// take the prefixed key and link to it. Saves; returns how many.
        /// </summary>
        public static int AdoptUnprefixed(IDbContext db, CardModel template, string addonKey)
        {
            var prefix = addonKey + "_";
            if (!template.IsTemplate || template.Key == null || !template.Key.StartsWith(prefix) || template.Key.Length == prefix.Length)
                return 0;

            var bare = template.Key[prefix.Length..];
            var keys = db.Properties.Where(p => p.Name == KeyProperty && p.Value == bare).ToList();
            var cardIds = CardsOf(db, template.GameId, keys.Select(p => p.ParentID).ToList());
            if (cardIds.Count == 0)
                return 0;

            var templates = TemplateIds(db, template.GameId);
            var links = db.Properties.Where(p => p.Name == IdProperty && cardIds.Contains(p.ParentID)).ToList();
            foreach (var key in keys.Where(k => cardIds.Contains(k.ParentID)))
            {
                var link = links.FirstOrDefault(p => p.ParentID == key.ParentID);
                if (link != null && Guid.TryParse(link.Value, out var current) && templates.Contains(current))
                    continue; // its template is still here
                key.Value = template.Key;
            }
            db.SaveChanges();
            return LinkOrphans(db, template);
        }

        /// <summary>The template in this game the card's link points at: by id, else by key.</summary>
        public static CardModel? Resolve(IDbContext db, Guid gameId, string? templateId, string? templateKey)
        {
            if (Guid.TryParse(templateId, out var id))
            {
                var byId = db.Cards.FirstOrDefault(c => c.Id == id && c.GameId == gameId && c.IsTemplate);
                if (byId != null)
                    return byId;
            }
            return string.IsNullOrWhiteSpace(templateKey)
                ? null
                : db.Cards.FirstOrDefault(c => c.GameId == gameId && c.IsTemplate && c.Key == templateKey);
        }

        private static void RememberKey(IDbContext db, Guid gameId, IEnumerable<Guid> templateIds, Func<Guid, string?> keyOf)
        {
            var values = templateIds.Select(id => id.ToString()).ToList();
            if (values.Count == 0)
                return;
            var links = db.Properties.Where(p => p.Name == IdProperty && p.Value != null && values.Contains(p.Value)).ToList();
            var cardIds = CardsOf(db, gameId, links.Select(p => p.ParentID).ToList());
            var keys = db.Properties.Where(p => p.Name == KeyProperty && cardIds.Contains(p.ParentID)).ToList();
            foreach (var link in links.Where(l => cardIds.Contains(l.ParentID)))
            {
                var key = keyOf(Guid.Parse(link.Value!));
                var existing = keys.FirstOrDefault(p => p.ParentID == link.ParentID);
                if (string.IsNullOrWhiteSpace(key))
                {
                    if (existing != null) db.Properties.Remove(existing);
                }
                else
                {
                    Set(db, link.ParentID, KeyProperty, key, existing);
                }
            }
            db.SaveChanges();
        }

        // The ordinary cards (not templates or views) of the game among these ids.
        private static List<Guid> CardsOf(IDbContext db, Guid gameId, List<Guid> ids) =>
            ids.Count == 0
                ? new List<Guid>()
                : db.Cards.Where(c => ids.Contains(c.Id) && c.GameId == gameId && !c.IsTemplate && !c.IsCustomUi).Select(c => c.Id).ToList();

        private static HashSet<Guid> TemplateIds(IDbContext db, Guid gameId) =>
            db.Cards.Where(c => c.GameId == gameId && c.IsTemplate).Select(c => c.Id).ToHashSet();

        private static void Set(IDbContext db, Guid cardId, string name, string value, PropertyModel? existing)
        {
            if (existing != null)
            {
                existing.Value = value;
                return;
            }
            db.Properties.Add(new PropertyModel
            {
                Id = Guid.NewGuid(),
                ParentID = cardId,
                Name = name,
                Value = value,
                EntityName = "CardModel",
                Card = db.Cards.Find(cardId),
            });
        }
    }
}

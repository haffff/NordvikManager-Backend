using AutoMapper;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TurnOrder
{
    internal class TurnOrderCommandHandler : HandlerBase<TurnOrderCommand, (CommandResponse, TurnOrderNotice?)>
    {
        public TurnOrderCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, TurnOrderNotice?)> Handle(TurnOrderCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var map = await dbContext.Maps.FirstOrDefaultAsync(m => m.Id == request.MapId && m.Game.Id == request.GameId, cancellationToken)
                ?? throw new ResourceNotFoundException("Map", request.MapId);
            var playerId = request.Player?.Id ?? Guid.Empty;

            var order = await dbContext.TurnOrders.Include(t => t.Entries).FirstOrDefaultAsync(t => t.MapId == map.Id, cancellationToken);

            if (request.Operation == TurnOrderOperation.EndTurn)
                await CheckCanEndTurn(map, order, playerId);
            else
                map.ThrowIfNoPermission(playerId, Permission.Edit);

            if (order == null)
            {
                order = new TurnOrderModel { MapId = map.Id };
                dbContext.TurnOrders.Add(order);
            }

            var before = (order.CurrentEntryId, order.Round);
            var notice = new TurnOrderNotice();

            switch (request.Operation)
            {
                case TurnOrderOperation.Add:
                    notice.AddedEntryIds = await AddEntries(order, request);
                    break;
                case TurnOrderOperation.Update:
                    Update(order, request);
                    break;
                case TurnOrderOperation.Remove:
                    var ids = request.EntryIds ?? new List<Guid>();
                    var elementIds = request.ElementIds ?? new List<Guid>();
                    TurnOrderRules.Remove(order, order.Entries.Where(e => ids.Contains(e.Id) || (e.ElementId.HasValue && elementIds.Contains(e.ElementId.Value))).ToList(), dbContext);
                    break;
                case TurnOrderOperation.Reorder:
                    Reorder(order, request.EntryIds ?? new List<Guid>());
                    break;
                case TurnOrderOperation.Sort:
                    Sort(order);
                    break;
                case TurnOrderOperation.Advance when request.EntryId.HasValue:
                    if (order.Entries.All(e => e.Id != request.EntryId))
                        throw new WrongArgumentsException(nameof(request.EntryId));
                    order.CurrentEntryId = request.EntryId;
                    break;
                case TurnOrderOperation.Advance:
                    TurnOrderRules.Step(order, request.Direction);
                    break;
                case TurnOrderOperation.EndTurn:
                    TurnOrderRules.Step(order, 1);
                    break;
                case TurnOrderOperation.Reset:
                    Reset(order, request.Clear);
                    break;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var current = order.Entries.FirstOrDefault(e => e.Id == order.CurrentEntryId);
            notice.MapId = map.Id;
            notice.Round = order.Round;
            notice.CurrentEntryId = current is { Hidden: false } ? current.Id : null;
            notice.ElementId = current is { Hidden: false } ? current.ElementId : null;
            notice.TurnChanged = before != (order.CurrentEntryId, order.Round);
            return (CommandResponse.Ok, notice);
        }

        /// <summary>The GM (Edit on the map), or the player controlling the current entry's token.</summary>
        private async Task CheckCanEndTurn(MapModel map, TurnOrderModel? order, Guid playerId)
        {
            if (map.HasPermission(playerId, Permission.Edit))
                return;

            var current = order?.Entries.FirstOrDefault(e => e.Id == order.CurrentEntryId);
            var token = current?.ElementId is Guid elementId
                ? await dbContext.Elements.FirstOrDefaultAsync(e => e.Id == elementId)
                : null;
            if (token == null)
                map.ThrowIfNoPermission(playerId, Permission.Edit);
            else
                token.ThrowIfNoPermission(playerId, Permission.Control);
        }

        private async Task<List<Guid>> AddEntries(TurnOrderModel order, TurnOrderCommand request)
        {
            var inputs = request.Entries ?? new List<TurnOrderEntryInput>();
            var elementIds = inputs.Where(i => i.ElementId.HasValue).Select(i => i.ElementId!.Value).Distinct().ToList();

            // Tokens must be on this map; their name is the default.
            var tokens = await dbContext.Elements
                .Where(e => elementIds.Contains(e.Id) && e.MapId == order.MapId)
                .Select(e => new { e.Id, Name = e.Details!.Where(d => d.Key == "name").Select(d => d.Value).FirstOrDefault() })
                .ToDictionaryAsync(e => e.Id, e => e.Name);
            var foreign = elementIds.Where(id => !tokens.ContainsKey(id)).ToList();
            if (foreign.Count > 0)
                throw new WrongArgumentsException(nameof(TurnOrderEntryInput.ElementId));

            var added = new List<Guid>();
            var position = order.Entries.Count == 0 ? 0 : order.Entries.Max(e => e.Position) + 1;
            foreach (var input in inputs)
            {
                if (input.ElementId is Guid elementId && order.Entries.Any(e => e.ElementId == elementId))
                    continue; // already in the order

                var name = input.Name ?? (input.ElementId is Guid id ? tokens[id] : null);
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (input.ElementId == null)
                        throw new WrongArgumentsException(nameof(TurnOrderEntryInput.Name));
                    name = "Token";
                }

                var entry = new TurnOrderEntryModel
                {
                    Id = Guid.NewGuid(),
                    Position = position++,
                    Name = name,
                    Initiative = input.Initiative,
                    ElementId = input.ElementId,
                    Hidden = input.Hidden ?? false,
                };
                order.Entries.Add(entry);
                dbContext.TurnOrderEntries.Add(entry);
                added.Add(entry.Id);
            }

            // The first entries start round 1 with the first one current.
            if (order.CurrentEntryId == null && order.Entries.Count > 0)
            {
                order.Round = 1;
                order.CurrentEntryId = TurnOrderRules.Ordered(order)[0].Id;
            }
            return added;
        }

        private static void Update(TurnOrderModel order, TurnOrderCommand request)
        {
            var entry = order.Entries.FirstOrDefault(e => request.EntryId.HasValue ? e.Id == request.EntryId : e.ElementId == request.ElementId && request.ElementId.HasValue)
                ?? throw new WrongArgumentsException(nameof(request.EntryId));
            if (!string.IsNullOrWhiteSpace(request.Name))
                entry.Name = request.Name;
            if (request.ClearInitiative)
                entry.Initiative = null;
            else if (request.Initiative.HasValue)
                entry.Initiative = request.Initiative;
            if (request.Hidden.HasValue)
                entry.Hidden = request.Hidden.Value;
            if (request.SortAfter)
                Sort(order);
        }

        private static void Reorder(TurnOrderModel order, List<Guid> entryIds)
        {
            // The listed entries first, in that order; any not listed keep their order after them.
            var ordered = entryIds
                .Select(id => order.Entries.FirstOrDefault(e => e.Id == id))
                .Where(e => e != null)
                .Select(e => e!)
                .Distinct()
                .ToList();
            ordered.AddRange(TurnOrderRules.Ordered(order).Where(e => !ordered.Contains(e)));
            TurnOrderRules.Renumber(ordered);
        }

        /// <summary>Highest initiative first; ties keep their place; no initiative last.</summary>
        private static void Sort(TurnOrderModel order)
        {
            var sorted = TurnOrderRules.Ordered(order)
                .OrderByDescending(e => e.Initiative.HasValue)
                .ThenByDescending(e => e.Initiative ?? 0)
                .ToList(); // OrderBy is stable
            TurnOrderRules.Renumber(sorted);
        }

        private void Reset(TurnOrderModel order, bool clear)
        {
            order.Round = 1;
            if (clear)
            {
                foreach (var entry in order.Entries.ToList())
                {
                    order.Entries.Remove(entry);
                    dbContext.TurnOrderEntries.Remove(entry);
                }
                order.CurrentEntryId = null;
                return;
            }
            order.CurrentEntryId = TurnOrderRules.Ordered(order).FirstOrDefault()?.Id;
        }
    }
}

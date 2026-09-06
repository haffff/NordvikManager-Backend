using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    internal class MoveCustomLayerCommandHandler : HandlerBase<MoveCustomLayerCommand, (CommandResponse, PropertyDTO)>
    {
        public MoveCustomLayerCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(MoveCustomLayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            Guard.Argument(request.Direction == 1 || request.Direction == -1, nameof(request.Direction));

            var gate = CustomLayerLayout.LockFor(request.GameId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                var game = dbContext.Games.Include(g => g.Properties).FirstOrDefault(g => g.Id == request.GameId);
                Guard.NotFound(game, "GameModel", request.GameId);

                game.ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

                var listProp = game.Properties.FirstOrDefault(p => p.Name == AddCustomLayerCommandHandler.ListPropertyName);
                Guard.NotFound(listProp, "Property", AddCustomLayerCommandHandler.ListPropertyName);

                var items = PropertyListJson.Deserialize(listProp.Value);
                var target = items.FirstOrDefault(i => i.Id == request.ItemId);
                Guard.NotFound(target, "PropertyListItem", request.ItemId);
                Guard.Argument(int.TryParse(target.Fields.GetValueOrDefault("layerId"), out var targetLayerId), "layerId");

                // Full ordered stack: the four reserved anchors plus every custom layer
                // with a parsable value, ascending. TokenUiLayerId is included even
                // though it's never shown/movable in the UI — it's still a real
                // boundary this logic must respect (a layer can't hop past it blindly).
                var merged = new List<(int Value, string? ItemId)>
                {
                    (CustomLayerLayout.MapLayerId, null),
                    (CustomLayerLayout.GridLayerId, null),
                    (CustomLayerLayout.TokenLayerId, null),
                    (CustomLayerLayout.TokenUiLayerId, null),
                };
                foreach (var item in items)
                {
                    if (int.TryParse(item.Fields.GetValueOrDefault("layerId"), out var v))
                        merged.Add((v, item.Id));
                }
                merged.Sort((a, b) => a.Value.CompareTo(b.Value));

                var idx = merged.FindIndex(x => x.ItemId == request.ItemId);
                var newIdx = idx + request.Direction;
                if (newIdx < 0 || newIdx >= merged.Count)
                {
                    // Already at an extreme — matches the disabled Move button in the
                    // UI, not a user-reachable path in practice. Silent no-op.
                    return (CommandResponse.Ok, mapper.Map<PropertyDTO>(listProp));
                }

                // newIdx is the target's desired FINAL rank (in merged's original,
                // full-length indexing). Since withoutTarget is merged with exactly one
                // element removed, re-inserting the target AT index newIdx in
                // withoutTarget reproduces that exact final arrangement regardless of
                // direction — e.g. moving idx=1 up to newIdx=2 in [A,B,C,D] must yield
                // [A,C,B,D]; withoutTarget=[A,C,D], and Insert(2, B) is what produces it
                // (Insert(1, B) would be a no-op). This holds symmetrically moving down.
                var withoutTarget = new List<(int Value, string? ItemId)>(merged);
                withoutTarget.RemoveAt(idx);
                var insertAt = newIdx;

                // Band resolution is direction-aware: when the target's new neighbor is
                // an anchor that doesn't cleanly bound a band on the side we need (e.g.
                // TokenLayerId/TokenUiLayerId around the dead 100-110 gap), which real
                // band is "correct" depends on which way the target is travelling —
                // the same reference value must resolve differently for a downward vs.
                // an upward move (moving down past TokenUi must land below it, in
                // Grid-Token; moving up past Token must land above the gap, in the top
                // band) — a value-only lookup can't distinguish those, so the two
                // directions read different neighbors with different rules:
                //   - Moving down: read the new UPPER neighbor via BandBelow (skips the
                //     dead gap downward when ambiguous). Always exists for a downward
                //     move — the target can never become the very topmost by moving down.
                //   - Moving up: read the new LOWER neighbor via BandFor (already skips
                //     the dead gap upward for an exact TokenLayerId/TokenUiLayerId
                //     reference — that's the same rule Add's AfterLayerId relies on).
                //     Always exists for an upward move — the target is never at merged
                //     index 0 (Map always is).
                var targetBand = request.Direction < 0
                    ? CustomLayerLayout.BandBelow(withoutTarget[insertAt].Value)
                    : CustomLayerLayout.BandFor(withoutTarget[insertAt - 1].Value);

                // Existing custom layers already in the target band, excluding the item
                // being moved, ordered bottom-to-top.
                var bandMembers = items
                    .Where(i => i.Id != request.ItemId)
                    .Select(i => (Item: i, LayerId: int.TryParse(i.Fields.GetValueOrDefault("layerId"), out var v) ? (int?)v : null))
                    .Where(x => x.LayerId.HasValue && x.LayerId.Value >= targetBand.Lo && x.LayerId.Value < targetBand.Hi)
                    .OrderBy(x => x.LayerId!.Value)
                    .ToList();

                // How many of those existing band members sit before the target's new
                // slot in the full merged ordering — computed by direct membership
                // count over the prefix rather than a value-threshold comparison, so it
                // can't misfire on the same boundary edge cases BandBelow exists to
                // handle correctly.
                var bandMemberIds = bandMembers.Select(x => x.Item.Id).ToHashSet();
                var bandInsertIndex = withoutTarget.Take(insertAt).Count(x => x.ItemId != null && bandMemberIds.Contains(x.ItemId));

                var orderedForRenormalize = new List<(string ItemId, int? CurrentLayerId)>();
                for (var i = 0; i < bandMembers.Count; i++)
                {
                    if (i == bandInsertIndex)
                        orderedForRenormalize.Add((request.ItemId, targetLayerId));
                    orderedForRenormalize.Add((bandMembers[i].Item.Id, bandMembers[i].LayerId));
                }
                if (bandInsertIndex == bandMembers.Count)
                    orderedForRenormalize.Add((request.ItemId, targetLayerId));

                var changes = CustomLayerLayout.Renormalize(orderedForRenormalize, targetBand);

                var oldValueByItemId = bandMembers.ToDictionary(x => x.Item.Id, x => x.LayerId!.Value);
                oldValueByItemId[request.ItemId] = targetLayerId;

                var reassignments = new Dictionary<int, int>();
                foreach (var (itemId, newLayerId) in changes)
                {
                    items.First(i => i.Id == itemId).Fields["layerId"] = newLayerId.ToString();
                    reassignments[oldValueByItemId[itemId]] = newLayerId;
                }

                listProp.Value = PropertyListJson.Serialize(items);

                if (reassignments.Count > 0)
                {
                    var affectedLayerIds = reassignments.Keys.ToList();
                    var affectedElements = dbContext.Elements
                        .Where(e => e.Map.Game.Id == request.GameId && affectedLayerIds.Contains(e.Layer))
                        .ToList();
                    foreach (var element in affectedElements)
                    {
                        element.Layer = reassignments[element.Layer];
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                return (CommandResponse.Ok, mapper.Map<PropertyDTO>(listProp));
            }
            finally
            {
                gate.Release();
            }
        }
    }
}

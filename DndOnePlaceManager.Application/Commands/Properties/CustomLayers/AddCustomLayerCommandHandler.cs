using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    internal class AddCustomLayerCommandHandler : HandlerBase<AddCustomLayerCommand, (CommandResponse, PropertyDTO)>
    {
        internal const string ListPropertyName = "customLayers";

        public AddCustomLayerCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(AddCustomLayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            Guard.Argument(!string.IsNullOrWhiteSpace(request.Name), nameof(request.Name));

            var gate = CustomLayerLayout.LockFor(request.GameId);
            await gate.WaitAsync(cancellationToken);
            try
            {
                var game = dbContext.Games.Include(g => g.Properties).FirstOrDefault(g => g.Id == request.GameId);
                Guard.NotFound(game, "GameModel", request.GameId);

                game.ThrowIfNoPermission(request.Player?.Id ?? default, Permission.Edit);

                var listProp = game.Properties.FirstOrDefault(p => p.Name == ListPropertyName);
                if (listProp == null)
                {
                    listProp = new PropertyModel { Name = ListPropertyName, EntityName = "GameModel", ParentID = game.Id, Value = "[]" };
                    game.Properties.Add(listProp);
                }

                var items = PropertyListJson.Deserialize(listProp.Value);

                var targetBand = request.AfterLayerId.HasValue
                    ? CustomLayerLayout.BandFor(request.AfterLayerId.Value)
                    : CustomLayerLayout.Bands[^1];

                // Existing custom layers already in the target band, ordered bottom-to-top.
                var bandMembers = items
                    .Select(i => (Item: i, LayerId: int.TryParse(i.Fields.GetValueOrDefault("layerId"), out var v) ? (int?)v : null))
                    .Where(x => x.LayerId.HasValue && x.LayerId.Value >= targetBand.Lo && x.LayerId.Value < targetBand.Hi)
                    .OrderBy(x => x.LayerId!.Value)
                    .ToList();

                // Insert immediately above the highest existing band member whose value
                // is <= AfterLayerId (0 / bottom-of-band if none qualify), or at the very
                // top of the band when AfterLayerId is null.
                var insertIndex = request.AfterLayerId.HasValue
                    ? bandMembers.FindLastIndex(x => x.LayerId!.Value <= request.AfterLayerId.Value) + 1
                    : bandMembers.Count;

                var newItemId = Guid.NewGuid().ToString("N");

                var orderedForRenormalize = new List<(string ItemId, int? CurrentLayerId)>();
                for (var i = 0; i < bandMembers.Count; i++)
                {
                    if (i == insertIndex)
                        orderedForRenormalize.Add((newItemId, null));
                    orderedForRenormalize.Add((bandMembers[i].Item.Id, bandMembers[i].LayerId));
                }
                if (insertIndex == bandMembers.Count)
                    orderedForRenormalize.Add((newItemId, null));

                var changes = CustomLayerLayout.Renormalize(orderedForRenormalize, targetBand);

                // Old layer id -> new layer id, for every EXISTING member whose value
                // changed (the brand-new item has no elements yet, nothing to sweep for it).
                var oldValueByItemId = bandMembers.ToDictionary(x => x.Item.Id, x => x.LayerId!.Value);
                var reassignments = new Dictionary<int, int>();

                foreach (var (itemId, newLayerId) in changes)
                {
                    if (itemId == newItemId)
                    {
                        items.Add(new PropertyListItemDTO
                        {
                            Id = newItemId,
                            Fields = new Dictionary<string, string?> { ["name"] = request.Name, ["layerId"] = newLayerId.ToString() },
                        });
                    }
                    else
                    {
                        items.First(i => i.Id == itemId).Fields["layerId"] = newLayerId.ToString();
                        reassignments[oldValueByItemId[itemId]] = newLayerId;
                    }
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

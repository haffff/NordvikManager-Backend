using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Guards;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.Properties
{
    internal class RemoveCustomLayerCommandHandler : HandlerBase<RemoveCustomLayerCommand, (CommandResponse, PropertyDTO)>
    {
        public RemoveCustomLayerCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<(CommandResponse, PropertyDTO)> Handle(RemoveCustomLayerCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

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
                var item = items.FirstOrDefault(i => i.Id == request.ItemId);
                Guard.NotFound(item, "PropertyListItem", request.ItemId);

                Guard.Argument(int.TryParse(item.Fields.GetValueOrDefault("layerId"), out var layerId), "layerId");

                // Deliberately not renormalizing the layer's former band on removal —
                // leftover uneven spacing self-heals next time that band gets an
                // Add/Move, and this avoids churning elements on unrelated layers.
                items.Remove(item);
                listProp.Value = PropertyListJson.Serialize(items);

                // Same transaction as the list update — an element can never end up
                // referencing a layer id that no longer appears in the layer list.
                var affectedElements = dbContext.Elements
                    .Where(e => e.Map.Game.Id == request.GameId && e.Layer == layerId)
                    .ToList();
                foreach (var element in affectedElements)
                {
                    element.Layer = CustomLayerLayout.MapLayerId;
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

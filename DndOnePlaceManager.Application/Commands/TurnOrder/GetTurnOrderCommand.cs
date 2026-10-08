using AutoMapper;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.Commands.TurnOrder
{
    /// <summary>
    /// A map's turn order (empty if it has none yet). Needs Read on the map. Without Edit
    /// on it, hidden entries are left out, and a hidden current entry only shows as
    /// CurrentHidden.
    /// </summary>
    public class GetTurnOrderCommand : CommandBase<TurnOrderDto?>
    {
        public Guid GameId { get; set; }
        public Guid MapId { get; set; }
        public PlayerDTO Player { get; set; } = null!;
    }

    internal class GetTurnOrderCommandHandler : HandlerBase<GetTurnOrderCommand, TurnOrderDto?>
    {
        public GetTurnOrderCommandHandler(IDbContext dbContext, IMapper mapper) : base(dbContext, mapper)
        {
        }

        public override async Task<TurnOrderDto?> Handle(GetTurnOrderCommand request, CancellationToken cancellationToken)
        {
            await base.Handle(request, cancellationToken);

            var map = await dbContext.Maps.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MapId && m.Game.Id == request.GameId, cancellationToken)
                ?? throw new ResourceNotFoundException("Map", request.MapId);
            var playerId = request.Player?.Id ?? Guid.Empty;
            map.ThrowIfNoPermission(playerId, Permission.Read);
            var seesHidden = map.HasPermission(playerId, Permission.Edit);

            var order = await dbContext.TurnOrders.AsNoTracking().Include(t => t.Entries)
                .FirstOrDefaultAsync(t => t.MapId == map.Id, cancellationToken);
            if (order == null)
                return new TurnOrderDto { MapId = map.Id, CanEdit = seesHidden };

            var current = order.Entries.FirstOrDefault(e => e.Id == order.CurrentEntryId);
            var currentHidden = !seesHidden && current is { Hidden: true };

            // Same rule as EndTurn: the GM, or whoever controls the current token.
            var canEndTurn = current != null && (seesHidden || (current.ElementId is Guid elementId
                && await dbContext.Elements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == elementId, cancellationToken) is { } token
                && token.HasPermission(playerId, Permission.Control)));

            return new TurnOrderDto
            {
                MapId = map.Id,
                Round = order.Round,
                CurrentEntryId = currentHidden ? null : order.CurrentEntryId,
                CurrentHidden = currentHidden,
                CanEndTurn = canEndTurn,
                CanEdit = seesHidden,
                Entries = order.Entries
                    .Where(e => seesHidden || !e.Hidden)
                    .OrderBy(e => e.Position)
                    .Select(e => new TurnOrderEntryDto { Id = e.Id, Name = e.Name, Initiative = e.Initiative, ElementId = e.ElementId, Hidden = e.Hidden })
                    .ToList(),
            };
        }
    }
}

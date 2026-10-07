using DndOnePlaceManager.Application.Commands.Elements;
using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Elements
{
    public class DeleteElementCommandHandlerTests : SqliteHandlerTestBase
    {
        [Fact]
        public async Task Handle_TokenInATurnOrder_LeavesTheOrder()
        {
            Guid mapId, token, other;
            using (var seed = SeedContext())
            {
                var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
                var map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = game };
                var tokenElement = new ElementModel { Id = Guid.NewGuid(), Map = map, Selectable = true };
                var otherElement = new ElementModel { Id = Guid.NewGuid(), Map = map, Selectable = true };
                var order = new TurnOrderModel { MapId = map.Id, Map = map };
                var first = new TurnOrderEntryModel { Id = Guid.NewGuid(), Position = 0, Name = "Goblin", ElementId = tokenElement.Id, TurnOrder = order };
                order.Entries.Add(first);
                order.Entries.Add(new TurnOrderEntryModel { Id = Guid.NewGuid(), Position = 1, Name = "Hero", ElementId = otherElement.Id, TurnOrder = order });
                order.CurrentEntryId = first.Id;
                seed.Games.Add(game);
                seed.Maps.Add(map);
                seed.Elements.AddRange(tokenElement, otherElement);
                seed.TurnOrders.Add(order);
                seed.Permissions.Add(new PermissionModel { ModelID = tokenElement.Id, PlayerID = PlayerId, Permission = Permission.All });
                seed.SaveChanges();
                (mapId, token, other) = (map.Id, tokenElement.Id, otherElement.Id);
            }

            var response = await new DeleteElementCommandHandler(Db, Mapper)
                .Handle(new DeleteElementCommand { Id = token, Player = new PlayerDTO { Id = PlayerId, Name = "GM" } }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var left = await SeedContext().TurnOrders.Include(t => t.Entries).SingleAsync(t => t.MapId == mapId);
            var remaining = Assert.Single(left.Entries);
            Assert.Equal(other, remaining.ElementId);
            Assert.Equal(remaining.Id, left.CurrentEntryId); // the turn passed on
        }
    }
}

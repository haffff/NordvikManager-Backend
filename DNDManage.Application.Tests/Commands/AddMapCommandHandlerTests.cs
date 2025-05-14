using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DndOnePlaceManager.Application.Commands.Map.AddMap
{
    public class AddMapCommandHandlerTests
    {
        [Fact]
        public async Task Handle_WithValidRequest_ReturnsOkResponseAndMapId()
        {
            // Arrange
            var dbContextMock = new Mock<IDbContext>();
            var game = new GameModel { Id = Guid.NewGuid(), Maps = new List<MapModel>() };
            dbContextMock.Setup(db => db.Games).Returns(MockDbSet(new List<GameModel> { game }));
            var handler = new AddMapCommandHandler(dbContextMock.Object);
            var request = new AddMapCommand { GameID = game.Id, Player = new PlayerModel { Id = Guid.NewGuid() } };

            // Act
            var (response, mapId) = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotEqual(Guid.Empty, mapId);
            Assert.Single(game.Maps);
            Assert.Equal(mapId, game.Maps.First().Id);
        }

        [Fact]
        public async Task Handle_WithInvalidGameID_ReturnsWrongArgumentsResponseAndEmptyMapId()
        {
            // Arrange
            var dbContextMock = new Mock<IDbContext>();
            dbContextMock.Setup(db => db.Games).Returns(MockDbSet(new List<GameModel>()));
            var handler = new AddMapCommandHandler(dbContextMock.Object);
            var request = new AddMapCommand { GameID = Guid.NewGuid(), Player = new PlayerModel { Id = Guid.NewGuid() } };

            // Act
            var (response, mapId) = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.WrongArguments, response);
            Assert.Equal(Guid.Empty, mapId);
        }

        [Fact]
        public async Task Handle_WithNoPermission_ReturnsNoPermissionResponseAndEmptyMapId()
        {
            // Arrange
            var dbContextMock = new Mock<IDbContext>();
            var game = new GameModel { Id = Guid.NewGuid(), Maps = new List<MapModel>() };
            dbContextMock.Setup(db => db.Games).Returns(MockDbSet(new List<GameModel> { game }));
            var handler = new AddMapCommandHandler(dbContextMock.Object);
            var request = new AddMapCommand { GameID = game.Id, Player = new PlayerModel { Id = Guid.NewGuid() } };
            game.SetPermissions(request.Player.Id, Permission.View); // Set permission to View only

            // Act
            var (response, mapId) = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.NoPermission, response);
            Assert.Equal(Guid.Empty, mapId);
        }

        private static DbSet<T> MockDbSet<T>(List<T> data) where T : class
        {
            var queryable = data.AsQueryable();
            var dbSetMock = new Mock<DbSet<T>>();
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            dbSetMock.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
            return dbSetMock.Object;
        }
    }
}

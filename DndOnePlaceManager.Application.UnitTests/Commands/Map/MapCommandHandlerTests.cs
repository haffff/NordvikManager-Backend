using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.Commands.Map.RemoveMap;
using DndOnePlaceManager.Application.Commands.Map.UpdateMap;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Map
{
    // =========================================================================
    // AddMapCommandHandler
    // =========================================================================
    public class AddMapCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public AddMapCommandHandlerTests()
        {
            // Tree-entry side-effect always succeeds
            _mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.FromResult<(CommandResponse, List<DndOnePlaceManager.Application.DataTransferObjects.TreeEntryDto>)>((CommandResponse.Ok, new List<DndOnePlaceManager.Application.DataTransferObjects.TreeEntryDto>())));
        }

        private AddMapCommandHandler Handler() =>
            new AddMapCommandHandler(Db, Mapper, _mediator.Object);

        [Fact]
        public async Task Handle_ValidRequest_ReturnsOkAndNewMapId()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new AddMapCommand
            {
                GameID = game.Id,
                Player = Player()
            };

            // Act
            var (response, mapId) = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotEqual(Guid.Empty, mapId);
        }

        [Fact]
        public async Task Handle_ValidRequest_MapIsPersistedInDatabase()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new AddMapCommand { GameID = game.Id, Player = Player() };

            // Act
            var (_, mapId) = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.True(Db.Maps.Any(m => m.Id == mapId));
        }

        [Fact]
        public async Task Handle_ValidRequest_UsesDefaultMapValues_WhenDtoIsNull()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new AddMapCommand { GameID = game.Id, Player = Player(), Dto = null };

            // Act
            var (response, mapId) = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            var map = Db.Maps.Find(mapId);
            Assert.NotNull(map);
            Assert.Equal("New map", map!.Name);
            Assert.Equal(50, map.GridSize);
            Assert.True(map.GridVisible);
            Assert.Equal(1200, map.Width);
            Assert.Equal(700, map.Height);
        }

        [Fact]
        public async Task Handle_WithExplicitDto_PersistsProvidedName()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new AddMapCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new MapDTO { Name = "Custom Map", GridSize = 100, Width = 800, Height = 600, Elements = new List<ElementDTO>() }
            };

            // Act
            var (response, mapId) = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            var map = Db.Maps.Find(mapId);
            Assert.Equal("Custom Map", map!.Name);
        }

        [Fact]
        public async Task Handle_UnknownGameId_ThrowsWrongArgumentsException()
        {
            // Arrange
            var cmd = new AddMapCommand
            {
                GameID = Guid.NewGuid(), // no such game
                Player = Player()
            };

            // Act & Assert
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_SendsAddTreeEntryCommand_AfterMapIsCreated()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new AddMapCommand { GameID = game.Id, Player = Player() };

            // Act
            await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            _mediator.Verify(
                m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    // =========================================================================
    // GetMapCommandHandler
    // =========================================================================
    public class GetMapCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IPermissionService> _permSvc;

        public GetMapCommandHandlerTests()
        {
            _permSvc = PermissionsMock; // already configured to grant all
        }

        private GetMapCommandHandler Handler() =>
            new GetMapCommandHandler(Db, Mapper, _permSvc.Object);

        private MapModel SeedMap(GameModel game, string name = "Test Map")
        {
            var mapId = Guid.NewGuid();
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            var map = new MapModel
            {
                Id = mapId, Name = name, GridSize = 50, GridVisible = true,
                Width = 1200, Height = 700, Game = g,
                Elements = new List<ElementModel>(),
                Properties = new List<DNDOnePlaceManager.Domain.Entities.BattleMap.PropertyModel>()
            };
            Db.Maps.Add(map);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return Db.Maps.Find(mapId)!;
        }

        [Fact]
        public async Task Handle_ExistingMap_ReturnsMapDTO()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game, "My Map");
            var cmd = new GetMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(map.Id, result!.Id);
            Assert.Equal("My Map", result.Name);
        }

        [Fact]
        public async Task Handle_NonExistingMap_ReturnsNull()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new GetMapCommand { Id = Guid.NewGuid(), GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_WhenPermissionDenied_ReturnsNull()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);

            // Override: deny all permissions for this test; re-anchor static SP so the
            // handler resolves THIS test's mock (not one set by a concurrently-constructed class)
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);
            PermissionsExtension.ServiceProvider = TestServiceProvider;

            var cmd = new GetMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_ReturnsCorrectDimensions()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            map.Width = 2000;
            map.Height = 1000;
            Db.SaveChanges();
            var cmd = new GetMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(2000, result!.Width);
            Assert.Equal(1000, result.Height);
        }
    }

    // =========================================================================
    // GetFlatMapsCommandHandler
    // =========================================================================
    public class GetFlatMapsCommandHandlerTests : HandlerTestBase
    {
        private GetFlatMapsCommandHandler Handler() =>
            new GetFlatMapsCommandHandler(Db, Mapper);

        private void SeedMaps(GameModel game, int count)
        {
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            for (int i = 0; i < count; i++)
            {
                Db.Maps.Add(new MapModel
                {
                    Id = Guid.NewGuid(), Name = $"Map {i}", GridSize = 50, Game = g,
                    Elements = new List<ElementModel>(),
                    Properties = new List<DNDOnePlaceManager.Domain.Entities.BattleMap.PropertyModel>()
                });
            }
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        [Fact]
        public async Task Handle_ReturnsAllMapsForGame()
        {
            // Arrange
            var game = BuildGame();
            SeedMaps(game, 3);
            var cmd = new GetFlatMapsCommand { GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);
        }

        [Fact]
        public async Task Handle_ReturnsFlatDtos_WithoutElements()
        {
            // Arrange
            var game = BuildGame();
            SeedMaps(game, 2);
            var cmd = new GetFlatMapsCommand { GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert — ModifyOutput strips everything except Name and Id
            Assert.All(result!, dto =>
            {
                Assert.NotNull(dto.Name);
                Assert.NotNull(dto.Id);
                Assert.Null(dto.Elements);
                Assert.Null(dto.Width);
                Assert.Null(dto.Height);
            });
        }

        [Fact]
        public async Task Handle_UnknownGame_ReturnsEmptyList()
        {
            // Arrange
            var cmd = new GetFlatMapsCommand { GameID = Guid.NewGuid(), Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        public async Task Handle_GameWithNoMaps_ReturnsEmptyList()
        {
            // Arrange
            var game = BuildGame(); // no maps added
            var cmd = new GetFlatMapsCommand { GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result!);
        }
    }

    // =========================================================================
    // UpdateMapCommandHandler
    // =========================================================================
    public class UpdateMapCommandHandlerTests : HandlerTestBase
    {
        private UpdateMapCommandHandler Handler() =>
            new UpdateMapCommandHandler(Db, Mapper);

        private MapModel SeedMap(GameModel game)
        {
            var mapId = Guid.NewGuid();
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            var map = new MapModel
            {
                Id = mapId, Name = "Original", GridSize = 50, GridVisible = true,
                Width = 800, Height = 600, Game = g,
                Elements = new List<ElementModel>(),
                Properties = new List<DNDOnePlaceManager.Domain.Entities.BattleMap.PropertyModel>()
            };
            Db.Maps.Add(map);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return Db.Maps.Find(mapId)!;
        }

        [Fact]
        public async Task Handle_ValidUpdate_ReturnsOk()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new UpdateMapCommand
            {
                GameId = game.Id,
                Player = Player(),
                Map = new MapDTO { Id = map.Id, Name = "Updated", GridSize = 100, Width = 1920, Height = 1080 }
            };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_ValidUpdate_PersistsChanges()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new UpdateMapCommand
            {
                GameId = game.Id,
                Player = Player(),
                Map = new MapDTO { Id = map.Id, Name = "Renamed Map", Width = 1600, Height = 900 }
            };

            // Act
            await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            var updated = Db.Maps.Find(map.Id);
            Assert.Equal("Renamed Map", updated!.Name);
            Assert.Equal(1600, updated.Width);
            Assert.Equal(900, updated.Height);
        }

        [Fact]
        public async Task Handle_UnknownGame_ThrowsResourceNotFoundException()
        {
            // Arrange
            var cmd = new UpdateMapCommand
            {
                GameId = Guid.NewGuid(),
                Player = Player(),
                Map = new MapDTO { Id = Guid.NewGuid(), Name = "X" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_UnknownMap_ThrowsResourceNotFoundException()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new UpdateMapCommand
            {
                GameId = game.Id,
                Player = Player(),
                Map = new MapDTO { Id = Guid.NewGuid(), Name = "Ghost" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NullNameInDto_KeepsOriginalName()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new UpdateMapCommand
            {
                GameId = game.Id,
                Player = Player(),
                Map = new MapDTO { Id = map.Id, Name = null, Width = 500, Height = 400 }
            };

            // Act
            await Handler().Handle(cmd, CancellationToken.None);

            // Assert — null Name should fall back to the original
            var updated = Db.Maps.Find(map.Id);
            Assert.Equal("Original", updated!.Name);
        }
    }

    // =========================================================================
    // RemoveMapCommandHandler
    // =========================================================================
    public class RemoveMapCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public RemoveMapCommandHandlerTests()
        {
            // Tree-entry removal always succeeds
            _mediator.Setup(m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
        }

        private RemoveMapCommandHandler Handler() =>
            new RemoveMapCommandHandler(Db, Mapper, _mediator.Object);

        private MapModel SeedMap(GameModel game)
        {
            var mapId = Guid.NewGuid();
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            var map = new MapModel
            {
                Id = mapId, Name = "To Delete", GridSize = 50, Game = g,
                Elements = new List<ElementModel>(),
                Properties = new List<DNDOnePlaceManager.Domain.Entities.BattleMap.PropertyModel>()
            };
            Db.Maps.Add(map);
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return Db.Maps.Find(mapId)!;
        }

        [Fact]
        public async Task Handle_ExistingMap_ReturnsOk()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new RemoveMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            var result = await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, result);
        }

        [Fact]
        public async Task Handle_ExistingMap_IsRemovedFromDatabase()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new RemoveMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Null(Db.Maps.Find(map.Id));
        }

        [Fact]
        public async Task Handle_ExistingMap_SendsRemoveTreeEntryCommand()
        {
            // Arrange
            var game = BuildGame();
            var map = SeedMap(game);
            var cmd = new RemoveMapCommand { Id = map.Id, GameID = game.Id, Player = Player() };

            // Act
            await Handler().Handle(cmd, CancellationToken.None);

            // Assert
            _mediator.Verify(
                m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistingMap_ThrowsResourceNotFoundException()
        {
            // Arrange
            var game = BuildGame();
            var cmd = new RemoveMapCommand { Id = Guid.NewGuid(), GameID = game.Id, Player = Player() };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}

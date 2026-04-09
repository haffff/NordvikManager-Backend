using AutoMapper;
using DndOnePlaceManager.Application;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Map.GetFlatMaps;
using DndOnePlaceManager.Application.Commands.Map.GetMap;
using DndOnePlaceManager.Application.Commands.Map.RemoveMap;
using DndOnePlaceManager.Application.Commands.Map.UpdateMap;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DNDManage.Application.Tests.Commands
{
    // =========================================================================
    // Shared base — one fresh InMemory database + AutoMapper + permissive
    // IPermissionService per test instance.
    // =========================================================================

    public abstract class MapHandlerTestBase : IDisposable
    {
        protected readonly DndOneContext Db;
        protected readonly IMapper Mapper;
        protected readonly Mock<IPermissionService> PermissionMock;
        protected readonly PlayerDTO AnyPlayer;
        protected readonly GameModel SeedGame;

        protected MapHandlerTestBase()
        {
            // Each test class gets its own unique InMemory database
            var options = new DbContextOptionsBuilder<DndOneContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Db = new DndOneContext(options);

            // Real AutoMapper with production profile - using explicit configuration
            var mapperConfig = new MapperConfiguration(cfg => 
            {
                cfg.AddProfile<AutoMapperProfile>();
            }, null);
            Mapper = mapperConfig.CreateMapper();

            // IPermissionService that always grants everything
            PermissionMock = new Mock<IPermissionService>();
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<PlayerDTO>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);
            PermissionMock.Setup(p => p.GetPermission(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<bool>()))
                .Returns(Permission.All);
            PermissionMock.Setup(p => p.GetPermission(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .Returns(Permission.All);
            PermissionMock.Setup(p => p.SetGenericPermissions(It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);
            PermissionMock.Setup(p => p.SetPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission?>()))
                .Returns(true);
            PermissionMock.Setup(p => p.UnsetPermission(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);

            // Wire the static service provider that PermissionsExtension uses
            var services = new ServiceCollection();
            services.AddSingleton(PermissionMock.Object);
            PermissionsExtension.ServiceProvider = services.BuildServiceProvider();

            // Seed a game that all tests can use
            AnyPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "Player" };
            SeedGame = new GameModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Game",
                Maps = new List<MapModel>(),
                Players = new List<PlayerModel>
                {
                    new PlayerModel { Id = AnyPlayer.Id!.Value, Name = AnyPlayer.Name }
                },
                SystemPlayerId = Guid.NewGuid()
            };
            Db.Games.Add(SeedGame);
            Db.SaveChanges();
        }

        public void Dispose() => Db.Dispose();
    }

    // =========================================================================
    // AddMapCommandHandler
    // =========================================================================

    public class AddMapCommandHandlerTests : MapHandlerTestBase
    {
        private AddMapCommandHandler CreateHandler(IMediator? mediator = null)
        {
            mediator ??= CreatePermissiveMediatorForAddTree();
            return new AddMapCommandHandler(Db, Mapper, mediator);
        }

        /// <summary>
        /// Returns a mediator mock whose AddTreeEntryCommand returns Ok,
        /// which is the tree-entry side-effect called after the map is saved.
        /// </summary>
        private static IMediator CreatePermissiveMediatorForAddTree()
        {
            var m = new Mock<IMediator>();
            m.Setup(x => x.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((CommandResponse.Ok, new List<TreeEntryDto>()));
            return m.Object;
        }

        // -----------------------------------------------------------------

        [Fact]
        public async Task Handle_ValidRequest_NullDto_UsesDefault_ReturnsOk()
        {
            // Arrange — no Dto supplied; handler uses GetDefault()
            var handler = CreateHandler();
            var cmd = new AddMapCommand
            {
                GameID = SeedGame.Id,
                Player = AnyPlayer,
                Dto = null
            };

            // Act
            var (response, mapId) = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotEqual(Guid.Empty, mapId);
        }

        [Fact]
        public async Task Handle_ValidRequest_WithDto_StoresSuppliedValues()
        {
            // Arrange
            var handler = CreateHandler();
            var dto = new MapDTO { Name = "Battle Arena", Width = 800, Height = 600, GridSize = 30, GridVisible = false };
            var cmd = new AddMapCommand { GameID = SeedGame.Id, Player = AnyPlayer, Dto = dto };

            // Act
            var (response, mapId) = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            var saved = Db.Maps.Find(mapId);
            Assert.NotNull(saved);
            Assert.Equal("Battle Arena", saved!.Name);
            Assert.Equal(800, saved.Width);
            Assert.Equal(600, saved.Height);
        }

        [Fact]
        public async Task Handle_DefaultDto_HasExpectedDefaults()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new AddMapCommand { GameID = SeedGame.Id, Player = AnyPlayer, Dto = null };

            // Act
            var (_, mapId) = await handler.Handle(cmd, CancellationToken.None);

            // Assert — values from AddMapCommandHandler.GetDefault()
            var saved = Db.Maps.Find(mapId);
            Assert.NotNull(saved);
            Assert.Equal("New map", saved!.Name);
            Assert.Equal(50, saved.GridSize);
            Assert.True(saved.GridVisible);
            Assert.Equal(1200, saved.Width);
            Assert.Equal(700, saved.Height);
        }

        [Fact]
        public async Task Handle_UnknownGameId_ReturnsWrongArguments()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new AddMapCommand { GameID = Guid.NewGuid(), Player = AnyPlayer };

            // Act
            var (response, mapId) = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.WrongArguments, response);
            Assert.Equal(Guid.Empty, mapId);
        }

        [Fact]
        public async Task Handle_WhenTreeEntryFails_ReturnsWrongArguments()
        {
            // Arrange — mediator returns error for tree-entry creation
            var failMediator = new Mock<IMediator>();
            failMediator.Setup(x => x.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((CommandResponse.WrongArguments, new List<TreeEntryDto>()));

            var handler = CreateHandler(failMediator.Object);
            var cmd = new AddMapCommand { GameID = SeedGame.Id, Player = AnyPlayer, Dto = null };

            // Act
            var (response, _) = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.WrongArguments, response);
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            // Arrange — override permission check to deny Edit
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), Permission.Edit))
                .Returns(false);

            var handler = CreateHandler();
            var cmd = new AddMapCommand { GameID = SeedGame.Id, Player = AnyPlayer, Dto = null };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.PermissionException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }
    }

    // =========================================================================
    // GetMapCommandHandler
    // =========================================================================

    public class GetMapCommandHandlerTests : MapHandlerTestBase
    {
        private readonly MapModel _seededMap;

        public GetMapCommandHandlerTests()
        {
            _seededMap = new MapModel
            {
                Id = Guid.NewGuid(),
                Name = "Forest Map",
                Width = 1000,
                Height = 800,
                GridSize = 50,
                GridVisible = true,
                Elements = new List<ElementModel>(),
                Properties = new List<PropertyModel>()
            };
            SeedGame.Maps.Add(_seededMap);
            Db.SaveChanges();
        }

        private GetMapCommandHandler CreateHandler()
        {
            var permService = PermissionMock.Object;
            return new GetMapCommandHandler(Db, Mapper, permService);
        }

        // -----------------------------------------------------------------

        [Fact]
        public async Task Handle_ExistingMap_ReturnsMapDto()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new GetMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_seededMap.Id, result!.Id);
            Assert.Equal("Forest Map", result.Name);
        }

        [Fact]
        public async Task Handle_UnknownMapId_ReturnsNull()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new GetMapCommand { Id = Guid.NewGuid(), GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NoPermission_ReturnsNull()
        {
            // Arrange — deny Read on map
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), Permission.Read))
                .Returns(false);

            var handler = CreateHandler();
            var cmd = new GetMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_ReturnsCorrectDimensions()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new GetMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(1000, result!.Width);
            Assert.Equal(800, result.Height);
            Assert.Equal(50, result.GridSize);
            Assert.True(result.GridVisible);
        }
    }

    // =========================================================================
    // GetFlatMapsCommandHandler
    // =========================================================================

    public class GetFlatMapsCommandHandlerTests : MapHandlerTestBase
    {
        private GetFlatMapsCommandHandler CreateHandler() =>
            new GetFlatMapsCommandHandler(Db, Mapper);

        // -----------------------------------------------------------------

        [Fact]
        public async Task Handle_NoMaps_ReturnsEmptyList()
        {
            // Arrange — SeedGame has no maps
            var handler = CreateHandler();
            var cmd = new GetFlatMapsCommand { GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        public async Task Handle_MapsExist_ReturnsCorrectCount()
        {
            // Arrange
            SeedGame.Maps.Add(new MapModel { Id = Guid.NewGuid(), Name = "Map A", Elements = new(), Properties = new() });
            SeedGame.Maps.Add(new MapModel { Id = Guid.NewGuid(), Name = "Map B", Elements = new(), Properties = new() });
            Db.SaveChanges();

            var handler = CreateHandler();
            var cmd = new GetFlatMapsCommand { GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(2, result!.Count);
        }

        [Fact]
        public async Task Handle_ResultsAreFlattenedWithoutElements()
        {
            // Arrange — map has an element, but flat result should not include it
            var map = new MapModel
            {
                Id = Guid.NewGuid(),
                Name = "Dungeon",
                Elements = new List<ElementModel>(),
                Properties = new()
            };
            SeedGame.Maps.Add(map);
            Db.SaveChanges();

            var handler = CreateHandler();
            var cmd = new GetFlatMapsCommand { GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert — flattened: only Name and Id are preserved
            Assert.Single(result!);
            Assert.Equal("Dungeon", result![0].Name);
            Assert.Equal(map.Id, result[0].Id);
            Assert.Null(result[0].Elements);
        }

        [Fact]
        public async Task Handle_UnknownGameId_ReturnsEmptyList()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new GetFlatMapsCommand { GameID = Guid.NewGuid(), Player = AnyPlayer };

            // Act
            var result = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result!);
        }
    }

    // =========================================================================
    // UpdateMapCommandHandler
    // =========================================================================

    public class UpdateMapCommandHandlerTests : MapHandlerTestBase
    {
        private readonly MapModel _seededMap;

        public UpdateMapCommandHandlerTests()
        {
            _seededMap = new MapModel
            {
                Id = Guid.NewGuid(),
                Name = "Old Name",
                Width = 1200,
                Height = 700,
                GridSize = 50,
                GridVisible = true,
                Elements = new(),
                Properties = new()
            };
            SeedGame.Maps.Add(_seededMap);
            Db.SaveChanges();
        }

        private UpdateMapCommandHandler CreateHandler() =>
            new UpdateMapCommandHandler(Db, Mapper);

        // -----------------------------------------------------------------

        [Fact]
        public async Task Handle_UpdateName_PersistsNewName()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = SeedGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Name = "New Name" }
            };

            // Act
            var response = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
            var updated = Db.Maps.Find(_seededMap.Id);
            Assert.Equal("New Name", updated!.Name);
        }

        [Fact]
        public async Task Handle_UpdateGridSize_PersistsNewGridSize()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = SeedGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, GridSize = 100 }
            };

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            var updated = Db.Maps.Find(_seededMap.Id);
            Assert.Equal(100, updated!.GridSize);
        }

        [Fact]
        public async Task Handle_UpdateDimensions_PersistsWidthAndHeight()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = SeedGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Width = 2000, Height = 1500 }
            };

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            var updated = Db.Maps.Find(_seededMap.Id);
            Assert.Equal(2000, updated!.Width);
            Assert.Equal(1500, updated.Height);
        }

        [Fact]
        public async Task Handle_UnknownGameId_ThrowsResourceNotFoundException()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = Guid.NewGuid(),
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Name = "X" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_MapNotInGame_ThrowsResourceNotFoundException()
        {
            // Arrange — map belongs to a different game
            var otherGame = new GameModel { Id = Guid.NewGuid(), Name = "Other", Maps = new(), Players = new(), SystemPlayerId = Guid.NewGuid() };
            Db.Games.Add(otherGame);
            Db.SaveChanges();

            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = otherGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Name = "X" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            // Arrange — deny Edit
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), Permission.Edit))
                .Returns(false);

            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = SeedGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Name = "Hacked" }
            };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.PermissionException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NullNameInDto_KeepsOriginalName()
        {
            // Arrange — null name → handler keeps original
            var handler = CreateHandler();
            var cmd = new UpdateMapCommand
            {
                GameId = SeedGame.Id,
                Player = AnyPlayer,
                Map = new MapDTO { Id = _seededMap.Id, Name = null }
            };

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            var updated = Db.Maps.Find(_seededMap.Id);
            Assert.Equal("Old Name", updated!.Name);
        }
    }

    // =========================================================================
    // RemoveMapCommandHandler
    // =========================================================================

    public class RemoveMapCommandHandlerTests : MapHandlerTestBase
    {
        private readonly MapModel _seededMap;

        public RemoveMapCommandHandlerTests()
        {
            _seededMap = new MapModel
            {
                Id = Guid.NewGuid(),
                Name = "Doomed Map",
                Elements = new(),
                Properties = new()
            };
            SeedGame.Maps.Add(_seededMap);
            Db.SaveChanges();
        }

        private RemoveMapCommandHandler CreateHandler(IMediator? mediator = null)
        {
            mediator ??= CreatePermissiveMediatorForRemoveTree();
            return new RemoveMapCommandHandler(Db, Mapper, mediator);
        }

        private static IMediator CreatePermissiveMediatorForRemoveTree()
        {
            var m = new Mock<IMediator>();
            m.Setup(x => x.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(CommandResponse.Ok);
            return m.Object;
        }

        // -----------------------------------------------------------------

        [Fact]
        public async Task Handle_ExistingMap_ReturnsOk()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new RemoveMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var response = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.Ok, response);
        }

        [Fact]
        public async Task Handle_ExistingMap_IsRemovedFromDatabase()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new RemoveMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            await handler.Handle(cmd, CancellationToken.None);

            // Assert
            var removed = Db.Maps.Find(_seededMap.Id);
            Assert.Null(removed);
        }

        [Fact]
        public async Task Handle_UnknownMapId_ThrowsResourceNotFoundException()
        {
            // Arrange
            var handler = CreateHandler();
            var cmd = new RemoveMapCommand { Id = Guid.NewGuid(), GameID = SeedGame.Id, Player = AnyPlayer };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.ResourceNotFoundException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NoPermission_ThrowsPermissionException()
        {
            // Arrange — deny Remove
            PermissionMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), Permission.Remove))
                .Returns(false);

            var handler = CreateHandler();
            var cmd = new RemoveMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act & Assert
            await Assert.ThrowsAsync<DndOnePlaceManager.Application.Exceptions.PermissionException>(
                () => handler.Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_WhenTreeEntryRemovalFails_ReturnsWrongArguments()
        {
            // Arrange — tree removal returns error after the delete succeeds
            var failMediator = new Mock<IMediator>();
            failMediator.Setup(x => x.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(CommandResponse.WrongArguments);

            var handler = CreateHandler(failMediator.Object);
            var cmd = new RemoveMapCommand { Id = _seededMap.Id, GameID = SeedGame.Id, Player = AnyPlayer };

            // Act
            var response = await handler.Handle(cmd, CancellationToken.None);

            // Assert
            Assert.Equal(CommandResponse.WrongArguments, response);
        }
    }
}

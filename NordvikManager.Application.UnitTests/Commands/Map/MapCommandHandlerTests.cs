using NordvikManager.Application.Commands.Folder.AddFolder;
using NordvikManager.Application.Commands.Map.AddMap;
using NordvikManager.Application.Commands.Map.GetFlatMaps;
using NordvikManager.Application.Commands.Map.GetMap;
using NordvikManager.Application.Commands.Map.RemoveMap;
using NordvikManager.Application.Commands.Map.UpdateMap;
using NordvikManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Extension;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Map
{
    // The map seeding the Get, Update and Remove tests share.
    public abstract class MapHandlerTestBase : HandlerTestBase
    {
        protected MapModel SeedMap(GameModel game, string name = "Test Map", int width = 1200, int height = 700)
        {
            var mapId = Guid.NewGuid();
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            Db.Maps.Add(NewMap(g, mapId, name, width, height));
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return Db.Maps.Find(mapId)!;
        }

        protected void SeedMaps(GameModel game, int count)
        {
            Db.ChangeTracker.Clear();
            var g = Db.Games.Find(game.Id)!;
            for (int i = 0; i < count; i++)
                Db.Maps.Add(NewMap(g, Guid.NewGuid(), $"Map {i}", 1200, 700));
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        private static MapModel NewMap(GameModel game, Guid id, string name, int width, int height) => new()
        {
            Id = id, Name = name, GridSize = 50, GridVisible = true,
            Width = width, Height = height, Game = game,
            Elements = new List<ElementModel>(),
            Properties = new List<NordvikManager.Domain.Entities.BattleMap.PropertyModel>()
        };
    }

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
                     .Returns(Task.FromResult<(CommandResponse, List<NordvikManager.Application.DataTransferObjects.TreeEntryDto>)>((CommandResponse.Ok, new List<NordvikManager.Application.DataTransferObjects.TreeEntryDto>())));
        }

        private Task<(CommandResponse, Guid)> Add(Guid gameId, MapDTO? dto = null) =>
            new AddMapCommandHandler(Db, Mapper, _mediator.Object).Handle(new AddMapCommand { GameID = gameId, Player = Player(), Dto = dto }, CancellationToken.None);

        [Fact]
        public async Task Handle_ValidRequest_PersistsTheMapAndAddsItsTreeEntry()
        {
            var game = BuildGame();

            var (response, mapId) = await Add(game.Id);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotEqual(Guid.Empty, mapId);
            Assert.True(Db.Maps.Any(m => m.Id == mapId));
            _mediator.Verify(
                m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // A solid grid was too harsh over real battle map images.
        [Fact]
        public async Task Handle_NewMap_GetsASemiTransparentGrid()
        {
            var game = BuildGame();

            var (_, mapId) = await Add(game.Id);

            Assert.Equal("rgba(170, 170, 170, 0.35)", Db.Maps.Find(mapId)!.GridColor);
        }

        [Fact]
        public async Task Handle_ValidRequest_UsesDefaultMapValues_WhenDtoIsNull()
        {
            var game = BuildGame();

            var (response, mapId) = await Add(game.Id, null);

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
            var game = BuildGame();

            var (response, mapId) = await Add(game.Id, new MapDTO { Name = "Custom Map", GridSize = 100, Width = 800, Height = 600, Elements = new List<ElementDTO>() });

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("Custom Map", Db.Maps.Find(mapId)!.Name);
        }

        [Fact]
        public async Task Handle_UnknownGameId_ThrowsWrongArgumentsException()
        {
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Add(Guid.NewGuid()));
        }
    }

    // =========================================================================
    // GetMapCommandHandler
    // =========================================================================
    public class GetMapCommandHandlerTests : MapHandlerTestBase
    {
        // PermissionsMock is already configured to grant all.
        private Task<MapDTO?> Get(GameModel game, Guid mapId) =>
            new GetMapCommandHandler(Db, Mapper, PermissionsMock.Object)
                .Handle(new GetMapCommand { Id = mapId, GameID = game.Id, Player = Player() }, CancellationToken.None);

        [Fact]
        public async Task Handle_ExistingMap_ReturnsMapDTO()
        {
            var game = BuildGame();
            var map = SeedMap(game, "My Map");

            var result = await Get(game, map.Id);

            Assert.NotNull(result);
            Assert.Equal(map.Id, result!.Id);
            Assert.Equal("My Map", result.Name);
        }

        [Fact]
        public async Task Handle_NonExistingMap_ReturnsNull()
        {
            var game = BuildGame();

            Assert.Null(await Get(game, Guid.NewGuid()));
        }

        [Fact]
        public async Task Handle_WhenPermissionDenied_ReturnsNull()
        {
            var game = BuildGame();
            var map = SeedMap(game);

            // Override: deny all permissions for this test; re-anchor static SP so the
            // handler resolves THIS test's mock (not one set by a concurrently-constructed class)
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(false);
            PermissionsExtension.ServiceProvider = TestServiceProvider;

            Assert.Null(await Get(game, map.Id));
        }

        [Fact]
        public async Task Handle_ReturnsCorrectDimensions()
        {
            var game = BuildGame();
            var map = SeedMap(game);
            map.Width = 2000;
            map.Height = 1000;
            Db.SaveChanges();

            var result = await Get(game, map.Id);

            Assert.Equal(2000, result!.Width);
            Assert.Equal(1000, result.Height);
        }
    }

    // =========================================================================
    // GetFlatMapsCommandHandler
    // =========================================================================
    public class GetFlatMapsCommandHandlerTests : MapHandlerTestBase
    {
        private Task<List<MapDTO>?> GetFlat(Guid gameId) =>
            new GetFlatMapsCommandHandler(Db, Mapper).Handle(new GetFlatMapsCommand { GameID = gameId, Player = Player() }, CancellationToken.None);

        [Fact]
        public async Task Handle_ReturnsAllMapsForGame()
        {
            var game = BuildGame();
            SeedMaps(game, 3);

            var result = await GetFlat(game.Id);

            Assert.NotNull(result);
            Assert.Equal(3, result!.Count);
        }

        [Fact]
        public async Task Handle_ReturnsFlatDtos_WithoutElements()
        {
            var game = BuildGame();
            SeedMaps(game, 2);

            var result = await GetFlat(game.Id);

            // ModifyOutput strips everything except Name and Id
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
            var result = await GetFlat(Guid.NewGuid());

            Assert.NotNull(result);
            Assert.Empty(result!);
        }

        [Fact]
        public async Task Handle_GameWithNoMaps_ReturnsEmptyList()
        {
            var game = BuildGame(); // no maps added

            var result = await GetFlat(game.Id);

            Assert.NotNull(result);
            Assert.Empty(result!);
        }
    }

    // =========================================================================
    // UpdateMapCommandHandler
    // =========================================================================
    public class UpdateMapCommandHandlerTests : MapHandlerTestBase
    {
        private Task<CommandResponse> Update(Guid gameId, MapDTO map) =>
            new UpdateMapCommandHandler(Db, Mapper).Handle(new UpdateMapCommand { GameId = gameId, Player = Player(), Map = map }, CancellationToken.None);

        [Fact]
        public async Task Handle_ValidUpdate_ReturnsOkAndPersistsChanges()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Original", 800, 600);

            var result = await Update(game.Id, new MapDTO { Id = map.Id, Name = "Renamed Map", GridSize = 100, Width = 1600, Height = 900 });

            Assert.Equal(CommandResponse.Ok, result);
            var updated = Db.Maps.Find(map.Id);
            Assert.Equal("Renamed Map", updated!.Name);
            Assert.Equal(1600, updated.Width);
            Assert.Equal(900, updated.Height);
        }

        [Fact]
        public async Task Handle_UnknownGame_ThrowsResourceNotFoundException()
        {
            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.ResourceNotFoundException>(
                () => Update(Guid.NewGuid(), new MapDTO { Id = Guid.NewGuid(), Name = "X" }));
        }

        [Fact]
        public async Task Handle_UnknownMap_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.ResourceNotFoundException>(
                () => Update(game.Id, new MapDTO { Id = Guid.NewGuid(), Name = "Ghost" }));
        }

        [Fact]
        public async Task Handle_NullNameInDto_KeepsOriginalName()
        {
            var game = BuildGame();
            var map = SeedMap(game, "Original", 800, 600);

            await Update(game.Id, new MapDTO { Id = map.Id, Name = null, Width = 500, Height = 400 });

            // null Name should fall back to the original
            Assert.Equal("Original", Db.Maps.Find(map.Id)!.Name);
        }
    }

    // =========================================================================
    // RemoveMapCommandHandler
    // =========================================================================
    public class RemoveMapCommandHandlerTests : MapHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public RemoveMapCommandHandlerTests()
        {
            // Tree-entry removal always succeeds
            _mediator.Setup(m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
        }

        private Task<CommandResponse> Remove(GameModel game, Guid mapId) =>
            new RemoveMapCommandHandler(Db, Mapper, _mediator.Object).Handle(new RemoveMapCommand { Id = mapId, GameID = game.Id, Player = Player() }, CancellationToken.None);

        [Fact]
        public async Task Handle_ExistingMap_IsRemovedWithItsTreeEntry()
        {
            var game = BuildGame();
            var map = SeedMap(game, "To Delete");

            var result = await Remove(game, map.Id);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.Null(Db.Maps.Find(map.Id));
            _mediator.Verify(
                m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task Handle_NonExistingMap_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<NordvikManager.Application.Exceptions.ResourceNotFoundException>(
                () => Remove(game, Guid.NewGuid()));
        }
    }
}

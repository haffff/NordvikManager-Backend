using AutoMapper;
using DndOnePlaceManager.Application.Extension;
using DndOnePlaceManager.Application.Services;
using DndOnePlaceManager.Domain.Entities.Interfaces;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests
{
    /// <summary>
    /// Shared base for handler tests — a fresh InMemory DB + real AutoMapper + a
    /// permissive <see cref="IPermissionService"/> mock, constructed fresh per test
    /// method (xUnit creates a new instance of the test class for every [Fact]).
    /// </summary>
    public abstract class HandlerTestBase : IDisposable
    {
        protected readonly DndOneContext Db;
        protected readonly IMapper Mapper;
        protected readonly Mock<IPermissionService> PermissionsMock;
        protected readonly Guid PlayerId = Guid.NewGuid();
        private readonly string _dbName = Guid.NewGuid().ToString();
        protected IServiceProvider TestServiceProvider { get; private set; }

        protected HandlerTestBase()
        {
            var options = new DbContextOptionsBuilder<DndOneContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            Db = new DndOneContext(options);

            // IPermissionService that always grants every permission
            PermissionsMock = new Mock<IPermissionService>();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);
            PermissionsMock.Setup(p => p.GetPermission(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<bool>()))
                .Returns(Permission.All);
            PermissionsMock.Setup(p => p.SetGenericPermissions(
                    It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);
            PermissionsMock.Setup(p => p.SetPermissions(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission?>()))
                .Returns(true);
            PermissionsMock.Setup(p => p.UnsetPermission(
                    It.IsAny<Guid>(), It.IsAny<IEntity>(), It.IsAny<Permission>()))
                .Returns(true);

            // Wire AutoMapper (v16 requires DI) and PermissionsExtension's service locator
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoMapper(x => x.AddProfile(typeof(AutoMapperProfile)));
            services.AddSingleton(PermissionsMock.Object);
            var sp = services.BuildServiceProvider();
            TestServiceProvider = sp;
            Mapper = sp.GetRequiredService<IMapper>();
            PermissionsExtension.ServiceProvider = sp;
        }

        // Build a game that already has the player in its Players list so that
        // GenericAddHandler.CheckPermissions / ThrowIfNoPermission pass.
        protected GameModel BuildGame(Guid? gameId = null)
        {
            var id = gameId ?? Guid.NewGuid();
            var game = new GameModel
            {
                Id = id,
                Name = "Test Game",
                SystemPlayerId = Guid.NewGuid(),
                Maps = new List<MapModel>(),
                Players = new List<PlayerModel>
                {
                    new PlayerModel { Id = PlayerId, Name = "Tester" }
                },
            };
            Db.Games.Add(game);
            Db.SaveChanges();
            return game;
        }

        protected DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO Player() =>
            new DndOnePlaceManager.Application.DataTransferObjects.Game.PlayerDTO { Id = PlayerId, Name = "Tester" };

        // A second context instance over the same InMemory DB, for reading back
        // state written by a handler under test without hitting the tracked instance.
        protected DndOneContext SeedContext() => new DndOneContext(
            new DbContextOptionsBuilder<DndOneContext>()
                .UseInMemoryDatabase(_dbName)
                .Options);

        public void Dispose() => Db.Dispose();
    }
}

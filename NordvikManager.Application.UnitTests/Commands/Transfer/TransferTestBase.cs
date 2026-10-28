using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Mapping;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Transfer
{
    /// <summary>
    /// Two games over one SQLite database, with the real mediator: export and import send
    /// the other commands (add map, add card, tree entries, resources) through it.
    /// </summary>
    public abstract class TransferTestBase : SqliteHandlerTestBase, IDisposable
    {
        protected readonly IMediator Mediator;
        protected readonly Mock<IFileStorageProvider> Storage = new();
        private readonly ServiceProvider mediatorServices;

        // PlayerId (from the base) runs the first game.
        protected readonly Guid GameId = Guid.NewGuid();
        protected readonly Guid SystemId = Guid.NewGuid();
        protected readonly Guid PlayerInGame = Guid.NewGuid();

        // A second game, run by someone else, with players of its own.
        protected readonly Guid OtherGameId = Guid.NewGuid();
        protected readonly Guid OtherGmId = Guid.NewGuid();
        protected readonly Guid OtherSystemId = Guid.NewGuid();

        protected PlayerDTO Gm => new() { Id = PlayerId, Name = "GM", IsOwner = true };
        protected PlayerDTO Player => new() { Id = PlayerInGame, Name = "Wizard" };
        protected PlayerDTO OtherGm => new() { Id = OtherGmId, Name = "Other GM", IsOwner = true };

        protected TransferTestBase()
        {
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<IMapper, AppMapper>();
            collection.AddSingleton<IDbContext>(Db);
            collection.AddScoped<IPermissionService, PermissionsService>();
            collection.AddSingleton(Storage.Object);
            collection.AddMediatR(typeof(ApplicationLayerModule).Assembly);
            mediatorServices = collection.BuildServiceProvider();
            Mediator = mediatorServices.GetRequiredService<IMediator>();

            using var seed = SeedContext();
            seed.Games.AddRange(
                new GameModel
                {
                    Id = GameId, Name = "Campaign", MasterId = PlayerId, SystemPlayerId = SystemId,
                    Players = new List<PlayerModel>
                    {
                        new PlayerModel { Id = PlayerId, Name = "GM" },
                        new PlayerModel { Id = SystemId, Name = "System", System = true },
                        new PlayerModel { Id = PlayerInGame, Name = "Wizard" },
                    },
                },
                new GameModel
                {
                    Id = OtherGameId, Name = "Other", MasterId = OtherGmId, SystemPlayerId = OtherSystemId,
                    Players = new List<PlayerModel>
                    {
                        new PlayerModel { Id = OtherGmId, Name = "Other GM" },
                        new PlayerModel { Id = OtherSystemId, Name = "System", System = true },
                    },
                });
            seed.Permissions.AddRange(
                Row(GameId, PlayerId, Permission.All),
                Row(GameId, SystemId, Permission.All),
                Row(GameId, PlayerInGame, Permission.Read),
                Row(OtherGameId, OtherGmId, Permission.All),
                Row(OtherGameId, OtherSystemId, Permission.All));
            seed.SaveChanges();
        }

        protected static PermissionModel Row(Guid model, Guid player, Permission permission, bool all = false) =>
            new() { Id = Guid.NewGuid(), ModelID = model, PlayerID = player, Permission = permission, All = all };

        protected bool Can(Guid player, Guid model, Permission permission) =>
            Permissions.CheckIfHasPermissions(new PlayerDTO { Id = player }, model, permission);

        public new void Dispose()
        {
            mediatorServices.Dispose();
            base.Dispose();
        }
    }
}

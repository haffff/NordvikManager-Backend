using NordvikManager.Application.Commands.Layouts.ForceLayout;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Layouts
{
    public class ForceLayoutCommandHandlerTests : HandlerTestBase
    {
        private ForceLayoutCommandHandler Handler() => new(Db, Mapper);

        private LayoutModel SeedLayout(NordvikManager.Domain.Entities.BattleMap.GameModel game, string name = "L1")
        {
            var layout = new LayoutModel { Id = Guid.NewGuid(), Name = name, Value = "{}", GameModelId = game.Id, Game = game };
            Db.Layouts.Add(layout);
            Db.SaveChanges();
            return layout;
        }

        [Fact]
        public async Task Handle_GmHasEditPermission_ReturnsOk()
        {
            var game = BuildGame();
            var layout = SeedLayout(game);
            var cmd = new ForceLayoutCommand { Player = Player(), GameID = game.Id, LayoutId = layout.Id };

            var response = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
        }

        [Fact]
        public async Task Handle_NoEditPermission_ThrowsPermissionException()
        {
            var game = BuildGame();
            var layout = SeedLayout(game);
            var strangerId = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(strangerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);
            var cmd = new ForceLayoutCommand { Player = new PlayerDTO { Id = strangerId }, GameID = game.Id, LayoutId = layout.Id };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new ForceLayoutCommand { Player = Player(), GameID = Guid.NewGuid(), LayoutId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_LayoutNotInGame_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            SeedLayout(game);
            var cmd = new ForceLayoutCommand { Player = Player(), GameID = game.Id, LayoutId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }
    }
}

using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // RunScript's Include runs a file's code on the server as the system, so it asks for
    // files from someone who may edit the game only: a player's upload under that key isn't one.
    public class GetResourceDataCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private GetResourceDataCommandHandler Handler() => new(Mapper, Db, Storage);

        private Guid PlayerWithoutEditOnTheGame()
        {
            var player = Guid.NewGuid();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(player, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => e is GameModel), It.IsAny<Permission>()))
                .Returns(false);
            return player;
        }

        [Fact]
        public async Task Handle_ByKey_ReturnsAnyonesFile()
        {
            var game = BuildGame();
            SeedResource(game, PlayerWithoutEditOnTheGame(), key: "notes.txt", data: new byte[] { 7 });

            var (data, _) = await Handler().Handle(new GetResourceDataCommand { GameID = game.Id, Player = Player(), Key = "notes.txt" }, CancellationToken.None);

            Assert.Equal(new byte[] { 7 }, data);
        }

        [Fact]
        public async Task Handle_OnlyFromGameEditors_IgnoresAPlayersUpload()
        {
            var game = BuildGame();
            SeedResource(game, PlayerWithoutEditOnTheGame(), key: "trilium_lib.js", data: new byte[] { 7 });

            var (data, _) = await Handler().Handle(new GetResourceDataCommand { GameID = game.Id, Player = Player(), Key = "trilium_lib.js", OnlyFromGameEditors = true }, CancellationToken.None);

            Assert.Null(data);
        }

        [Fact]
        public async Task Handle_OnlyFromGameEditors_ReturnsTheGmsFile()
        {
            var game = BuildGame();
            SeedResource(game, Guid.NewGuid(), key: "trilium_lib.js", data: new byte[] { 7 }); // the permissive mock: may edit the game

            var (data, _) = await Handler().Handle(new GetResourceDataCommand { GameID = game.Id, Player = Player(), Key = "trilium_lib.js", OnlyFromGameEditors = true }, CancellationToken.None);

            Assert.Equal(new byte[] { 7 }, data);
        }
    }
}

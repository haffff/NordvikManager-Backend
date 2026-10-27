using NordvikManager.Application.Commands.Properties.UpdateProperties;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Properties
{
    // properties/UpdateBulk (the settings panels). Each row is checked against the entity
    // it really belongs to, like a single update: the ids and parents in the request come
    // from the client.
    public class UpdatePropertiesCommandHandlerTests : HandlerTestBase
    {
        private UpdatePropertiesCommandHandler Handler() => new(Db, Mapper);

        private Guid SeedProperty(GameModel game, string name, string? value, bool isProtected = false)
        {
            var id = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel { Id = id, Name = name, Value = value, ParentID = game.Id, EntityName = "GameModel", IsProtected = isProtected });
            Db.SaveChanges();
            return id;
        }

        // A second game (BuildGame would add the same player twice).
        private GameModel OtherGame()
        {
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid(), Maps = new List<MapModel>(), Players = new List<PlayerModel>() };
            Db.Games.Add(game);
            Db.SaveChanges();
            return game;
        }

        private void DenyEditOn(GameModel game) =>
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(It.IsAny<Guid>(), It.Is<IEntity>(e => e.Id == game.Id), Permission.Edit))
                           .Returns(false);

        private UpdatePropertiesCommand Command(params PropertyDTO[] properties) =>
            new() { GameID = Guid.NewGuid(), Player = Player(), Properties = properties };

        private PropertyModel Stored(Guid id) => SeedContext().Properties.Single(p => p.Id == id);

        [Fact]
        public async Task Handle_UpdatesEveryRow()
        {
            var game = BuildGame();
            var a = SeedProperty(game, "A", "1");
            var b = SeedProperty(game, "B", "2");

            var result = await Handler().Handle(Command(
                new PropertyDTO { Id = a, Name = "A", Value = "10", ParentID = game.Id },
                new PropertyDTO { Id = b, Name = "B", Value = "20", ParentID = game.Id }), CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.Equal("10", Stored(a).Value);
            Assert.Equal("20", Stored(b).Value);
        }

        [Fact]
        public async Task Handle_ARowOfAnEntityThePlayerCannotEdit_Throws_AndSavesNothing()
        {
            var mine = BuildGame();
            var theirs = OtherGame();
            var own = SeedProperty(mine, "A", "1");
            var foreign = SeedProperty(theirs, "Secret", "keep");
            DenyEditOn(theirs);

            // The foreign row claims the editable parent; its real parent decides.
            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(Command(
                new PropertyDTO { Id = own, Name = "A", Value = "changed", ParentID = mine.Id },
                new PropertyDTO { Id = foreign, Name = "Secret", Value = "overwritten", ParentID = mine.Id }), CancellationToken.None));

            Assert.Equal("1", Stored(own).Value);
            Assert.Equal("keep", Stored(foreign).Value);
        }

        [Fact]
        public async Task Handle_DoesNotMoveARowToTheParentTheClientSent()
        {
            var game = BuildGame();
            var other = OtherGame();
            var id = SeedProperty(game, "A", "1");

            await Handler().Handle(Command(new PropertyDTO { Id = id, Name = "A", Value = "2", ParentID = other.Id }), CancellationToken.None);

            Assert.Equal(game.Id, Stored(id).ParentID);
            Assert.Equal("2", Stored(id).Value);
        }

        [Fact]
        public async Task Handle_UnknownRow_ThrowsNotFound()
        {
            var game = BuildGame();

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(
                Command(new PropertyDTO { Id = Guid.NewGuid(), Name = "A", Value = "2", ParentID = game.Id }), CancellationToken.None));
        }

        // Same rule as a single update: unprotecting never reveals the hidden value.
        [Fact]
        public async Task Handle_UnprotectingARow_WipesItsValue()
        {
            var game = BuildGame();
            var id = SeedProperty(game, "Secret", "hidden", isProtected: true);

            await Handler().Handle(Command(new PropertyDTO { Id = id, Name = "Secret", Value = "hidden", IsProtected = false, ParentID = game.Id }), CancellationToken.None);

            Assert.Null(Stored(id).Value);
            Assert.False(Stored(id).IsProtected);
        }
    }
}

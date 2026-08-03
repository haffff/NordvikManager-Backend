using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // Covers the (CommandResponse, PropertyDTO) return-type refactor this session —
    // PropertiesHandler.cs (WS layer) needs the returned DTO to re-broadcast a
    // canonical, correctly-cased payload instead of echoing the client's own data back.
    public class UpdatePropertyCommandHandlerTests : HandlerTestBase
    {
        private UpdatePropertyCommandHandler Handler() => new(Db, Mapper);

        private (GameModel game, Guid propertyId) SeedGameWithProperty(bool isProtected = false, string? value = "Easy")
        {
            var game = BuildGame();
            var propId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel
            {
                Id = propId,
                Name = "Difficulty",
                Value = value,
                ParentID = game.Id,
                EntityName = "GameModel",
                IsProtected = isProtected,
            });
            Db.SaveChanges();
            return (game, propId);
        }

        [Fact]
        public async Task Handle_PropertyNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UpdatePropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Id = Guid.NewGuid(), Name = "Difficulty", Value = "Hard" },
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUpdate_ReturnsOkAndPopulatedDto()
        {
            var (game, propId) = SeedGameWithProperty();
            var cmd = new UpdatePropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Id = propId, Name = "Difficulty", Value = "Hard", IsProtected = false },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(dto);
            Assert.Equal(propId, dto!.Id);
            Assert.Equal(game.Id, dto.ParentID);
            Assert.Equal("Hard", dto.Value);
        }

        [Fact]
        public async Task Handle_ProtectedToUnprotected_WipesStoredValue()
        {
            var (_, propId) = SeedGameWithProperty(isProtected: true, value: "secret");
            var cmd = new UpdatePropertyCommand
            {
                Player = Player(),
                // Attacker/client attempts to un-protect and simultaneously read the old
                // value back out via the same request — the handler must ignore the
                // supplied Value and wipe it instead of echoing the previously-hidden secret.
                Property = new PropertyDTO { Id = propId, Name = "Difficulty", Value = "leaked?", IsProtected = false },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Null(dto!.Value);
            Assert.False(dto.IsProtected);

            var reread = await Db.Properties.FindAsync(propId);
            Assert.Null(reread!.Value);
        }

        [Fact]
        public async Task Handle_StaysProtected_KeepsSuppliedValue()
        {
            var (_, propId) = SeedGameWithProperty(isProtected: true, value: "secret");
            var cmd = new UpdatePropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Id = propId, Name = "Difficulty", Value = "new-secret", IsProtected = true },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal("new-secret", dto!.Value);
        }
    }
}

using DndOnePlaceManager.Application.Commands.Properties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // Covers the (CommandResponse, PropertyDTO) return-type refactor this session —
    // previously Remove returned CommandResponse alone, so PropertiesHandler.cs's
    // WS broadcast for property_remove was just the bare property ID string, with no
    // parentId at all — making it impossible for clients to route the removal to the
    // right entity.
    public class RemovePropertyCommandHandlerTests : HandlerTestBase
    {
        private RemovePropertyCommandHandler Handler() => new(Db, Mapper);

        private (GameModel game, Guid propertyId) SeedGameWithProperty()
        {
            var game = BuildGame();
            var propId = Guid.NewGuid();
            Db.Properties.Add(new PropertyModel
            {
                Id = propId,
                Name = "Difficulty",
                Value = "Hard",
                ParentID = game.Id,
                EntityName = "GameModel",
            });
            Db.SaveChanges();
            return (game, propId);
        }

        [Fact]
        public async Task Handle_PropertyNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new RemovePropertyCommand { Player = Player(), Id = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidRemoval_ReturnsPopulatedDtoAndDeletesRow()
        {
            var (game, propId) = SeedGameWithProperty();
            var cmd = new RemovePropertyCommand { Player = Player(), Id = propId };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            // The DTO must be fully populated — it's captured before deletion so the
            // broadcast can still describe what was removed (name, parentId, ...) even
            // though the row itself is already gone by the time this returns.
            Assert.NotNull(dto);
            Assert.Equal(propId, dto!.Id);
            Assert.Equal(game.Id, dto.ParentID);
            Assert.Equal("Difficulty", dto.Name);

            var reread = await Db.Properties.FindAsync(propId);
            Assert.Null(reread);
        }
    }
}

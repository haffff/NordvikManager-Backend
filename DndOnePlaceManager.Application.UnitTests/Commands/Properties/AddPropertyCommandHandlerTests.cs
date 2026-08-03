using DndOnePlaceManager.Application.Commands.Properties.AddProperty;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    // Covers the (CommandResponse, PropertyDTO) return-type refactor this session —
    // previously AddPropertyCommandHandler returned (CommandResponse, Guid), which meant
    // PropertiesHandler.cs (the WS layer) had nothing to re-broadcast and fell back to
    // echoing the client's own payload verbatim, casing and all.
    public class AddPropertyCommandHandlerTests : HandlerTestBase
    {
        private AddPropertyCommandHandler Handler() => new(Db, Mapper);

        private GameModel SeedGameWithProperties(Func<Guid, PropertyModel>? existingFactory = null)
        {
            var game = BuildGame();
            game.Properties = new List<PropertyModel>();
            if (existingFactory != null)
            {
                var prop = existingFactory(game.Id);
                prop.Game = game;
                Db.Properties.Add(prop);
            }
            Db.SaveChanges();
            return game;
        }

        [Fact]
        public async Task Handle_EntityNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new AddPropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Name = "A", ParentID = Guid.NewGuid(), EntityName = "GameModel" },
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NewProperty_ReturnsOkAndPopulatedDto()
        {
            var game = SeedGameWithProperties();
            var cmd = new AddPropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Name = "Difficulty", Value = "Hard", ParentID = game.Id, EntityName = "GameModel" },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(dto);
            Assert.NotNull(dto!.Id);
            Assert.Equal(game.Id, dto.ParentID);
            Assert.Equal("Difficulty", dto.Name);
            Assert.Equal("Hard", dto.Value);

            var reread = Db.Games.Include(g => g.Properties).First(g => g.Id == game.Id);
            Assert.Single(reread.Properties!);
        }

        [Fact]
        public async Task Handle_DuplicateNameForSameParent_ReturnsAlreadyExistsAndNullDto()
        {
            var game = SeedGameWithProperties(gameId =>
                new PropertyModel { Id = Guid.NewGuid(), Name = "Difficulty", Value = "Easy", ParentID = gameId });

            var cmd = new AddPropertyCommand
            {
                Player = Player(),
                Property = new PropertyDTO { Name = "Difficulty", Value = "Hard", ParentID = game.Id, EntityName = "GameModel" },
            };

            var (response, dto) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.AlreadyExists, response);
            Assert.Null(dto);

            var reread = Db.Games.Include(g => g.Properties).First(g => g.Id == game.Id);
            Assert.Single(reread.Properties!);
            Assert.Equal("Easy", reread.Properties![0].Value); // untouched
        }
    }
}

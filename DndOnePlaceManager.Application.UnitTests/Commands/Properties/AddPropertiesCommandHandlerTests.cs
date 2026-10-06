using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Properties
{
    public class AddPropertiesCommandHandlerTests : HandlerTestBase
    {
        private AddPropertiesCommandHandler Handler() => new(Db, Mapper);

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
        public async Task Handle_EmptyProperties_ReturnsNoChange()
        {
            var cmd = new AddPropertiesCommand { GameID = Guid.NewGuid(), Player = Player(), Properties = Array.Empty<PropertyDTO>() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.NoChange, result);
        }

        [Fact]
        public async Task Handle_PropertiesWithDifferentParentIds_ThrowsWrongArgumentsException()
        {
            var game = SeedGameWithProperties();
            var cmd = new AddPropertiesCommand
            {
                GameID = game.Id,
                Player = Player(),
                Properties = new[]
                {
                    new PropertyDTO { Name = "A", ParentID = game.Id, EntityName = "GameModel" },
                    new PropertyDTO { Name = "B", ParentID = Guid.NewGuid(), EntityName = "GameModel" },
                },
            };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_EntityNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new AddPropertiesCommand
            {
                GameID = Guid.NewGuid(),
                Player = Player(),
                Properties = new[] { new PropertyDTO { Name = "A", ParentID = Guid.NewGuid(), EntityName = "GameModel" } },
            };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidGameProperties_AddsPropertiesAndReturnsOk()
        {
            var game = SeedGameWithProperties();
            var cmd = new AddPropertiesCommand
            {
                GameID = game.Id,
                Player = Player(),
                Properties = new[]
                {
                    new PropertyDTO { Name = "Difficulty", Value = "Hard", ParentID = game.Id, EntityName = "GameModel" },
                    new PropertyDTO { Name = "MaxPlayers", Value = "5", ParentID = game.Id, EntityName = "GameModel" },
                },
            };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            var reread = Db.Games.Include(g => g.Properties).First(g => g.Id == game.Id);
            Assert.Equal(2, reread.Properties!.Count);
            Assert.Contains(reread.Properties, p => p.Name == "Difficulty" && p.Value == "Hard");
        }

        [Fact]
        public async Task Handle_DuplicateNameForSameParent_IsNotAddedTwice()
        {
            var game = SeedGameWithProperties(gameId =>
                new PropertyModel { Id = Guid.NewGuid(), Name = "Difficulty", Value = "Easy", ParentID = gameId });

            var cmd = new AddPropertiesCommand
            {
                GameID = game.Id,
                Player = Player(),
                Properties = new[] { new PropertyDTO { Name = "Difficulty", Value = "Hard", ParentID = game.Id, EntityName = "GameModel" } },
            };

            await Handler().Handle(cmd, CancellationToken.None);

            var reread = Db.Games.Include(g => g.Properties).First(g => g.Id == game.Id);
            Assert.Single(reread.Properties!);
            Assert.Equal("Easy", reread.Properties![0].Value); // original untouched, new duplicate skipped
        }

        // Adding used to load every existing property of the owner (a character sheet card
        // has hundreds) just to append to its collection or check names.
        private GameModel SeedGameWithManyProperties(int count)
        {
            var game = SeedGameWithProperties();
            for (int i = 0; i < count; i++)
                Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = $"p{i}", Value = "v", ParentID = game.Id, EntityName = "GameModel", Game = game });
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
            return game;
        }

        private int CountPropertiesOf(Guid parentId)
        {
            using var check = SeedContext();
            return check.Properties.Count(p => p.ParentID == parentId);
        }

        [Fact]
        public async Task Handle_DoesNotLoadTheOwnersOtherProperties_AndStillSkipsExistingNames()
        {
            var game = SeedGameWithManyProperties(20);
            var cmd = new AddPropertiesCommand
            {
                GameID = game.Id, Player = Player(),
                Properties = new[]
                {
                    new PropertyDTO { Name = "new1", ParentID = game.Id, EntityName = "GameModel" },
                    new PropertyDTO { Name = "p3", Value = "changed", ParentID = game.Id, EntityName = "GameModel" },
                    new PropertyDTO { Name = "new2", ParentID = game.Id, EntityName = "GameModel" },
                },
            };

            Assert.Equal(CommandResponse.Ok, await Handler().Handle(cmd, CancellationToken.None));

            Assert.Equal(new[] { "new1", "new2" }, Db.ChangeTracker.Entries<PropertyModel>().Select(e => e.Entity.Name).OrderBy(x => x));
            Assert.Equal(22, CountPropertiesOf(game.Id));
            using var check = SeedContext();
            Assert.Equal("v", check.Properties.Single(p => p.ParentID == game.Id && p.Name == "p3").Value);
        }
    }
}

using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    // AddImageCommandHandler is the registered handler for AddResourceCommand
    // (see ApplicationLayerModule's reflection-based *CommandHandler discovery).
    public class AddResourceCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public AddResourceCommandHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AddTreeEntryCommand cmd, CancellationToken _) =>
                    (CommandResponse.Ok, new List<TreeEntryDto> { new TreeEntryDto { Id = Guid.NewGuid(), Name = cmd.TreeEntryDto.Name } }));
        }

        private AddImageCommandHandler Handler() => new(Db, Mapper, _mediator.Object, Storage);

        // Regression: a resource whose file extension isn't recognized by
        // StringEntityExtensions.ToMimeType() (e.g. ".gitkeep") resolves to
        // MimeType.None, which has no [Description] attribute — GetDescriptionValue()
        // returns null, so AddResourceCommand.MimeType arrives here as null. Before
        // the fix, ToEnumUsingDescriptionAttribute<TEnum> called value.ToLower() on
        // that null without a guard, throwing NullReferenceException and aborting
        // the ENTIRE addon install (not just this one resource) — reproduced live
        // via a leftover .gitkeep file shipped alongside real resources in an
        // addon's Resources/ folder.
        [Fact]
        public async Task Handle_UnrecognizedMimeType_FallsBackToNoneInsteadOfThrowing()
        {
            var game = BuildGame();
            var cmd = new AddResourceCommand
            {
                GameID = game.Id,
                Player = Player(),
                Name = ".gitkeep",
                MimeType = null,
                DataRaw = Array.Empty<byte>(),
                Key = "addon_.gitkeep",
            };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.NotNull(id);
            var created = Db.Resources.Find(id!.Value);
            Assert.Equal(MimeType.None, created!.MimeType);
        }

        [Fact]
        public async Task Handle_RecognizedMimeType_ParsesCorrectly()
        {
            var game = BuildGame();
            var cmd = new AddResourceCommand
            {
                GameID = game.Id,
                Player = Player(),
                Name = "attrBinding.js",
                MimeType = "text/javascript",
                DataRaw = new byte[] { 1, 2, 3 },
                Key = "addon_attrBinding.js",
            };

            var (response, id) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var created = Db.Resources.Find(id!.Value);
            Assert.Equal(MimeType.JavaScript, created!.MimeType);
        }

        // Regression: the handler loaded the game with all of its resources AND tree entries in
        // one joined query (resources × entries rows). On a game with an installed addon that
        // is tens of millions of rows; SQLite's sort spilled to temp and failed with "disk full".
        [Fact]
        public async Task Handle_DoesNotLoadTheGamesExistingResourcesOrTreeEntries()
        {
            var gameId = Guid.NewGuid();
            using (var seed = SeedContext())
            {
                var game = new GameModel
                {
                    Id = gameId, Name = "Big game", SystemPlayerId = Guid.NewGuid(),
                    Players = new List<PlayerModel> { new PlayerModel { Id = PlayerId, Name = "Tester" } },
                    TreeEntries = new List<TreeEntryModel>(),
                };
                seed.Games.Add(game);
                for (var i = 0; i < 3; i++)
                {
                    seed.Resources.Add(new ResourceModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = PlayerId, Name = $"r{i}", Data = new byte[] { 1 } });
                    game.TreeEntries.Add(new TreeEntryModel { Id = Guid.NewGuid(), Name = $"e{i}", EntryType = "ResourceModel" });
                }
                seed.SaveChanges();
            }

            var (response, id) = await Handler().Handle(new AddResourceCommand
            {
                GameID = gameId,
                Player = Player(),
                Name = "new.png",
                MimeType = "image/png",
                DataRaw = new byte[] { 1, 2, 3 },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(id, Assert.Single(Db.ChangeTracker.Entries<ResourceModel>()).Entity.Id);
            Assert.Empty(Db.ChangeTracker.Entries<TreeEntryModel>());
            using var check = SeedContext();
            Assert.Equal(gameId, check.Resources.Find(id!.Value)!.GameId);
        }
    }
}

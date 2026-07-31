using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Domain.Entities.Chat;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Chat
{
    public class GetMessagesCommandHandlerTests : HandlerTestBase
    {
        private GetMessagesCommandHandler Handler() => new(Db, Mapper);

        private MessageModel SeedMessage(Guid gameId, Guid playerId, string content, DateTime created)
        {
            var message = new MessageModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = playerId, Content = content, Created = created };
            Db.Messages.Add(message);
            Db.SaveChanges();
            return message;
        }

        [Fact]
        public async Task Handle_ReturnsOnlyMessagesForRequestedGame()
        {
            var game = BuildGame();
            var otherGameId = Guid.NewGuid();
            SeedMessage(game.Id, PlayerId, "in game", DateTime.UtcNow);
            SeedMessage(otherGameId, PlayerId, "other game", DateTime.UtcNow);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("in game", result[0].Data);
        }

        [Fact]
        public async Task Handle_FilterByContent_AppliesTextFilter()
        {
            var game = BuildGame();
            SeedMessage(game.Id, PlayerId, "hello world", DateTime.UtcNow);
            SeedMessage(game.Id, PlayerId, "goodbye", DateTime.UtcNow);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0, Filter = "hello" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("hello world", result[0].Data);
        }

        [Fact]
        public async Task Handle_FilterByFrom_FiltersByPlayer()
        {
            var game = BuildGame();
            var otherPlayerId = Guid.NewGuid();
            SeedMessage(game.Id, PlayerId, "mine", DateTime.UtcNow);
            SeedMessage(game.Id, otherPlayerId, "theirs", DateTime.UtcNow);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0, From = otherPlayerId };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Single(result);
            Assert.Equal("theirs", result[0].Data);
        }

        [Fact]
        public async Task Handle_OrdersByCreatedDescending()
        {
            var game = BuildGame();
            var now = DateTime.UtcNow;
            SeedMessage(game.Id, PlayerId, "first", now.AddMinutes(-10));
            SeedMessage(game.Id, PlayerId, "second", now);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal("second", result[0].Data);
            Assert.Equal("first", result[1].Data);
        }

        [Fact]
        public async Task Handle_Pagination_SkipsAndTakesRequestedPage()
        {
            var game = BuildGame();
            var now = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
                SeedMessage(game.Id, PlayerId, $"msg{i}", now.AddMinutes(-i));
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 2, Page = 1 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Equal("msg2", result[0].Data);
            Assert.Equal("msg3", result[1].Data);
        }

        [Fact]
        public async Task Handle_NoPermissionOnMessage_ExcludesFromResults()
        {
            var game = BuildGame();
            var message = SeedMessage(game.Id, PlayerId, "hidden", DateTime.UtcNow);
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.Is<DndOnePlaceManager.Domain.Entities.Interfaces.IEntity>(e => ((MessageModel)e).Id == message.Id), DndOnePlaceManager.Domain.Enums.Permission.Read))
                .Returns(false);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(result);
        }
    }
}

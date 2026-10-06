using DndOnePlaceManager.Application.Commands.Chat.GetMessages;
using DndOnePlaceManager.Domain.Entities.Chat;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Chat
{
    // SQLite-backed with the real permission service: filtering, permissions and paging
    // must all happen in SQL (this used to load every message of every game).
    public class GetMessagesCommandHandlerTests : SqliteHandlerTestBase
    {
        private GetMessagesCommandHandler Handler() => new(Db, Mapper);

        private sealed record GameRef(Guid Id);

        private GameRef BuildGame() => new(Guid.NewGuid());

        // Posted messages get an "everyone can read" row (AddMessageCommandHandler → SetGlobalPermission).
        private MessageModel SeedMessage(Guid gameId, Guid playerId, string content, DateTime created, bool everyoneCanRead = true)
        {
            var message = new MessageModel { Id = Guid.NewGuid(), GameId = gameId, PlayerId = playerId, Content = content, Created = created };
            using var seed = SeedContext();
            seed.Messages.Add(message);
            if (everyoneCanRead)
                seed.Permissions.Add(new PermissionModel { ModelID = message.Id, All = true, Permission = Permission.Read });
            seed.SaveChanges();
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
            SeedMessage(game.Id, PlayerId, "hidden", DateTime.UtcNow, everyoneCanRead: false);
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task Handle_PlayersOwnPermissionRowWinsOverEveryoneRow()
        {
            var game = BuildGame();
            var message = SeedMessage(game.Id, PlayerId, "hidden from me", DateTime.UtcNow);
            using (var seed = SeedContext())
            {
                seed.Permissions.Add(new PermissionModel { ModelID = message.Id, PlayerID = PlayerId, Permission = Permission.None });
                seed.SaveChanges();
            }
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 10, Page = 0 };

            Assert.Empty(await Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_HiddenMessagesDoNotTakeUpPageSlots()
        {
            var game = BuildGame();
            var now = DateTime.UtcNow;
            SeedMessage(game.Id, PlayerId, "newest, hidden", now, everyoneCanRead: false);
            SeedMessage(game.Id, PlayerId, "a", now.AddMinutes(-1));
            SeedMessage(game.Id, PlayerId, "b", now.AddMinutes(-2));
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 2, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(new[] { "a", "b" }, result.Select(x => x.Data));
        }

        [Fact]
        public async Task Handle_SendsOneQueryAndLoadsOnlyThePage()
        {
            var game = BuildGame();
            var otherGame = BuildGame();
            var now = DateTime.UtcNow;
            for (int i = 0; i < 40; i++)
            {
                SeedMessage(game.Id, PlayerId, $"msg{i}", now.AddMinutes(-i));
                SeedMessage(otherGame.Id, PlayerId, $"other{i}", now.AddMinutes(-i));
            }
            Commands.Reset();
            var cmd = new GetMessagesCommand { GameID = game.Id, PlayerID = PlayerId, Size = 5, Page = 0 };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(5, result.Count);
            Assert.Equal(1, Commands.Count);
            Assert.True(Db.ChangeTracker.Entries<MessageModel>().Count() <= 5);
        }
    }
}

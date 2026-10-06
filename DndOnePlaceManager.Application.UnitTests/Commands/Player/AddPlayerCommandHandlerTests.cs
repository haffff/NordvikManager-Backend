using DndOnePlaceManager.Application.Commands.BattleMap;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Player
{
    // Every joining player used to be named "Player".
    public class AddPlayerCommandHandlerTests : HandlerTestBase
    {
        private AddPlayerCommandHandler Handler() =>
            new(Db, Mapper, new Mock<IMediator>().Object, NullLogger<AddPlayerCommandHandler>.Instance);

        private string NameOfNewPlayer(User user)
        {
            var game = BuildGame();
            var id = Handler().Handle(new AddPlayerCommand { GameID = game.Id, User = user, SkipPasswordCheck = true }, CancellationToken.None).Result;
            using var check = SeedContext();
            return check.Players.Find(id!.Value)!.Name;
        }

        [Fact]
        public void Handle_NewPlayer_IsNamedAfterTheirUsername()
        {
            Assert.Equal("alice", NameOfNewPlayer(new User { Id = "central-1", UserName = "alice" }));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Handle_NoUsername_FallsBackToPlayer(string? userName)
        {
            Assert.Equal("Player", NameOfNewPlayer(new User { Id = "central-1", UserName = userName }));
        }
    }
}

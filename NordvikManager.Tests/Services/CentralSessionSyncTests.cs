using MediatR;
using Moq;
using NordvikManager.Application.Commands.Game.GetGameCentralSessionId;
using NordvikManager.Application.Commands.Game.GetGameSessionDetails;
using NordvikManager.Application.Commands.Game.SetGameCentralSession;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services;
using NordvikManager.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // Keeps a game's Central session in step with the game: made when there is none, its
    // listing (and password) re-sent on every start, and made again when Central expired or
    // deleted it.
    public class CentralSessionSyncTests
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<ICentralServerService> _central = new();
        private readonly Guid _gameId = Guid.NewGuid();

        public CentralSessionSyncTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetGameSessionDetailsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetGameSessionDetailsResult { Name = "Game", PasswordRequired = true, Password = "pw", Image = "img" });
            _central.Setup(c => c.CreateSessionAsync(It.IsAny<string>(), It.IsAny<GameItemDTO>())).ReturnsAsync("new-session");
            _central.Setup(c => c.UpdateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameItemDTO>()))
                    .ReturnsAsync(CentralSessionUpdate.Updated);
        }

        private CentralSessionSync Sync() => new(_mediator.Object, _central.Object);

        private void StoredSession(string? id) =>
            _mediator.Setup(m => m.Send(It.IsAny<GetGameCentralSessionIdCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(id);

        private void VerifySaved(string id, Times times) =>
            _mediator.Verify(m => m.Send(It.Is<SetGameCentralSessionCommand>(c => c.GameId == _gameId && c.CentralSessionId == id), It.IsAny<CancellationToken>()), times);

        [Fact]
        public async Task Ensure_CreatesAndStoresASession_WhenTheGameHasNone()
        {
            StoredSession(null);

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Equal("new-session", result.SessionId);
            _central.Verify(c => c.CreateSessionAsync("token", It.Is<GameItemDTO>(g =>
                g.Name == "Game" && g.PasswordRequired && g.Password == "pw" && g.Image == "img")), Times.Once);
            VerifySaved("new-session", Times.Once());
        }

        [Fact]
        public async Task Ensure_UpdatesTheExistingSessionWithThePassword()
        {
            StoredSession("old-session");

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Equal("old-session", result.SessionId);
            _central.Verify(c => c.UpdateSessionAsync("token", "old-session", It.Is<GameItemDTO>(g => g.Password == "pw" && g.Name == "Game")), Times.Once);
            _central.Verify(c => c.CreateSessionAsync(It.IsAny<string>(), It.IsAny<GameItemDTO>()), Times.Never);
        }

        [Fact]
        public async Task Ensure_MakesANewSession_WhenCentralNoLongerHasTheOldOne()
        {
            StoredSession("expired-session");
            _central.Setup(c => c.UpdateSessionAsync(It.IsAny<string>(), "expired-session", It.IsAny<GameItemDTO>()))
                    .ReturnsAsync(CentralSessionUpdate.Missing);

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Equal("new-session", result.SessionId);
            VerifySaved("new-session", Times.Once());
        }

        [Fact]
        public async Task Ensure_KeepsTheSession_WhenTheUpdateMerelyFailed()
        {
            // Unreachable or too old for the route: the session is still there, starting goes on.
            StoredSession("old-session");
            _central.Setup(c => c.UpdateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameItemDTO>()))
                    .ReturnsAsync(CentralSessionUpdate.Failed);

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Equal("old-session", result.SessionId);
            _central.Verify(c => c.CreateSessionAsync(It.IsAny<string>(), It.IsAny<GameItemDTO>()), Times.Never);
        }

        [Fact]
        public async Task Ensure_HasNoSession_WhenCreatingFails()
        {
            StoredSession(null);
            _central.Setup(c => c.CreateSessionAsync(It.IsAny<string>(), It.IsAny<GameItemDTO>())).ReturnsAsync((string?)null);

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Null(result.SessionId);
            Assert.NotNull(result.Details);
            VerifySaved("new-session", Times.Never());
        }

        [Fact]
        public async Task Ensure_HasNoDetails_WhenTheGameIsGone()
        {
            StoredSession("old-session");
            _mediator.Setup(m => m.Send(It.IsAny<GetGameSessionDetailsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((GetGameSessionDetailsResult?)null);

            var result = await Sync().EnsureAsync(_gameId, "token");

            Assert.Null(result.Details);
            Assert.Null(result.SessionId);
        }

        [Fact]
        public async Task Replace_MakesANewSessionAndDeletesTheOldOne()
        {
            StoredSession("old-session");

            var result = await Sync().ReplaceAsync(_gameId, "token");

            Assert.Equal("new-session", result.SessionId);
            VerifySaved("new-session", Times.Once());
            _central.Verify(c => c.DeleteSessionAsync("token", "old-session"), Times.Once);
        }

        [Fact]
        public async Task Replace_KeepsTheOldSession_WhenANewOneCannotBeMade()
        {
            StoredSession("old-session");
            _central.Setup(c => c.CreateSessionAsync(It.IsAny<string>(), It.IsAny<GameItemDTO>())).ReturnsAsync((string?)null);

            var result = await Sync().ReplaceAsync(_gameId, "token");

            Assert.Null(result.SessionId);
            _central.Verify(c => c.DeleteSessionAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}

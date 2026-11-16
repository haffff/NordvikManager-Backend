using NordvikManager.Services.Implementations;
using Xunit;

namespace NordvikManager.Tests.Services
{
    // GMs hear of a refused request once per action, origin and reason. The origin can come
    // from player input (a chat argument, a card field), so what the lobby remembers is capped:
    // past the cap nothing new is announced, and memory stays bounded while the game runs.
    public class GameLobbyNoticeTests
    {
        [Fact]
        public void FirstNotice_IsTrueOncePerKey()
        {
            var lobby = TestLobby.Create();

            Assert.True(lobby.FirstNotice("a"));
            Assert.False(lobby.FirstNotice("a"));
            Assert.True(lobby.FirstNotice("b"));
        }

        [Fact]
        public void FirstNotice_StopsRemembering_PastTheCap()
        {
            var lobby = TestLobby.Create();
            for (var i = 0; i < GameLobby.MaxNotices; i++)
                Assert.True(lobby.FirstNotice($"k{i}"));

            Assert.False(lobby.FirstNotice("one more"));
            Assert.False(lobby.FirstNotice("k0"));
        }
    }
}

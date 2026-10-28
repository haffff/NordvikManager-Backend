using NordvikManager.Application.Commands.Security.CheckPermissions;
using Moq;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations
{
    // playerIsGm: the player who ran the action is the owner or may edit the game (a co-GM).
    public class BuiltInVariablesTests : ActionServiceTestBase
    {
        [Fact]
        public async Task PlayerIsGm_ForAPlayerWhoMayEditTheGame()
        {
            AddAction("a", Step("s", "SetVariable", new { Name = "copy", Value = "%playerIsGm%" }));

            var msgs = await Run("a");

            Assert.Equal("true", Last(msgs, "s")["variables"]?["copy"]?.ToString());
        }

        [Fact]
        public async Task PlayerIsGm_FalseForAPlayer()
        {
            // Not traced for a player who can't edit the game, so check through the event log.
            _mediator.Setup(m => m.Send(It.IsAny<CheckPermissionsCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            AddAction("a", Step("s", "Log", new { Message = "gm=%playerIsGm%" }));

            await Run("a");

            Assert.Contains(_lobby.EventLog.GetEntries(), e => e.Message.Contains("gm=false"));
        }
    }
}

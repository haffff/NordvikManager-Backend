using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Tests.Services
{
    // An operation's outcome reaches its player as one push. When that push is lost, the
    // client's progress toast spun until its 10-minute timeout; it can now ask the server.
    public class OperationResultsTests
    {
        private DateTime _now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        private OperationResults Results() => new(() => _now);
        private readonly Guid _player = Guid.NewGuid();

        [Fact]
        public void Get_ReturnsRunning_AfterBegin()
        {
            var results = Results();
            var op = Guid.NewGuid();

            results.Begin(op, _player);

            Assert.Equal(OperationResults.Running, results.Get(op, _player)?.Status);
        }

        [Fact]
        public void Get_ReturnsOutcome_AfterFinish()
        {
            var results = Results();
            var done = Guid.NewGuid();
            var failed = Guid.NewGuid();
            results.Begin(done, _player);
            results.Begin(failed, _player);

            results.Finish(done, _player, false, "Addon installed", "Fifth Edition");
            results.Finish(failed, _player, true, "Installation failed", "zip is broken");

            var d = results.Get(done, _player)!;
            Assert.Equal(OperationResults.Complete, d.Status);
            Assert.Equal("Addon installed", d.Title);
            Assert.Equal("Fifth Edition", d.Description);
            Assert.Equal(OperationResults.Failed, results.Get(failed, _player)!.Status);
            Assert.Equal("zip is broken", results.Get(failed, _player)!.Description);
        }

        [Fact]
        public void Get_ReturnsNull_ForAnotherPlayer()
        {
            var results = Results();
            var op = Guid.NewGuid();
            results.Begin(op, _player);
            results.Finish(op, _player, true, "Installation failed", "C:\\secret\\path");

            Assert.Null(results.Get(op, Guid.NewGuid()));
        }

        [Fact]
        public void Get_ReturnsNull_ForUnknownOperation()
        {
            Assert.Null(Results().Get(Guid.NewGuid(), _player));
        }

        [Fact]
        public void Get_ReturnsNull_AfterAnHour()
        {
            var results = Results();
            var op = Guid.NewGuid();
            results.Begin(op, _player);
            results.Finish(op, _player, false, "Addon installed", null);

            _now = _now.AddMinutes(59);
            Assert.NotNull(results.Get(op, _player));

            _now = _now.AddMinutes(2);
            Assert.Null(results.Get(op, _player));
        }

        [Fact]
        public async Task Fail_RecordsOutcome_WithNoLobbyToPushTo()
        {
            var op = Guid.NewGuid();
            var player = new PlayerDTO { Id = _player, Name = "GM" };

            OperationProgressPublisher.Begin(op, player);
            Assert.Equal(OperationResults.Running, OperationResults.Shared.Get(op, _player)?.Status);

            await OperationProgressPublisher.Fail(null, player, op, "Installation failed", "boom");

            var result = OperationResults.Shared.Get(op, _player)!;
            Assert.Equal(OperationResults.Failed, result.Status);
            Assert.Equal("boom", result.Description);
        }
    }
}

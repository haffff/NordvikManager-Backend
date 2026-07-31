using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Layouts.AddLayout;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Game
{
    public class AddGameCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        public AddGameCommandHandlerTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = new PlayerDTO { Id = PlayerId, Name = "Game Master" } });
            _mediator.Setup(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, (Guid?)Guid.NewGuid()));
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
            _mediator.Setup(m => m.Send(It.IsAny<AddMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            _mediator.Setup(m => m.Send(It.IsAny<AddBattleMapCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            _mediator.Setup(m => m.Send(It.IsAny<AddLayoutCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, Guid.NewGuid()));
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync((CommandResponse.Ok, new InstallAddonCommandResponse()));
        }

        private AddGameCommandHandler Handler(IConfiguration? config = null) =>
            new(Db, Mapper, _mediator.Object,
                config ?? new ConfigurationBuilder().Build(),
                NullLogger<AddGameCommandHandler>.Instance);

        private static User ValidUser() => new() { Id = Guid.NewGuid().ToString(), UserName = "gm" };

        private static AddGameCommand ValidCommand() => new()
        {
            User = ValidUser(),
            Name = "My Game",
            PasswordRequired = false,
        };

        [Fact]
        public async Task Handle_PasswordRequiredButMissing_ReturnsNull()
        {
            var cmd = ValidCommand();
            cmd.PasswordRequired = true;
            cmd.Password = "  ";

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_InvalidUserId_ReturnsNull()
        {
            var cmd = ValidCommand();
            cmd.User = new User { Id = "not-a-guid", UserName = "gm" };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NullUser_ReturnsNull()
        {
            var cmd = ValidCommand();
            cmd.User = null;

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_ValidRequest_CreatesGameWithMasterAndSystemPlayers()
        {
            var cmd = ValidCommand();

            var gameId = await Handler().Handle(cmd, CancellationToken.None);

            Assert.NotNull(gameId);
            var game = Db.Games.Include(g => g.Players).First(g => g.Id == gameId);
            Assert.Equal("My Game", game.Name);
            Assert.Equal(2, game.Players.Count);
            Assert.Contains(game.Players, p => p.Name == "Game Master");
            Assert.Contains(game.Players, p => p.Name == "System" && p.System);
            Assert.Equal(game.Players.First(p => p.Name == "Game Master").Id, game.MasterId);
            Assert.Equal(game.Players.First(p => p.System).Id, game.SystemPlayerId);
        }

        [Fact]
        public async Task Handle_ValidRequest_SendsMapBattleMapAndLayoutCommands()
        {
            var cmd = ValidCommand();

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddMapCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<AddBattleMapCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<AddLayoutCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithImage_SendsAddResourceAndSetsImageProperty()
        {
            var cmd = ValidCommand();
            cmd.Image = "data:image/png;base64,aGVsbG8=";

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(
                It.Is<AddPropertiesCommand>(c => c.Properties.Any(p => p.Name == "image")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithoutImage_DoesNotSendAddResourceCommand()
        {
            var cmd = ValidCommand();

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<AddResourceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NoAddonsSelected_SkipsAddonInstall()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AddonsConfiguration:MainRepository"] = "https://repo.example/addons" })
                .Build();
            var cmd = ValidCommand();

            await Handler(config).Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AddonsSelectedWithRepositoryConfigured_InstallsEachAddon()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AddonsConfiguration:MainRepository"] = "https://repo.example/addons" })
                .Build();
            var cmd = ValidCommand();
            cmd.AddonsSelected = new[] { "dnd5e", "pathfinder" };

            await Handler(config).Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Handle_AddonsSelectedButNoRepositoryConfigured_SkipsInstall()
        {
            var cmd = ValidCommand();
            cmd.AddonsSelected = new[] { "dnd5e" };

            var gameId = await Handler().Handle(cmd, CancellationToken.None); // default config has no MainRepository key

            Assert.NotNull(gameId);
            _mediator.Verify(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AddonInstallThrows_SwallowsExceptionAndContinues()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AddonsConfiguration:MainRepository"] = "https://repo.example/addons" })
                .Build();
            _mediator.Setup(m => m.Send(It.IsAny<InstallAddonCommand>(), It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidOperationException("boom"));
            var cmd = ValidCommand();
            cmd.AddonsSelected = new[] { "dnd5e" };

            var gameId = await Handler(config).Handle(cmd, CancellationToken.None);

            Assert.NotNull(gameId); // handler must not propagate the per-addon failure
        }
    }
}

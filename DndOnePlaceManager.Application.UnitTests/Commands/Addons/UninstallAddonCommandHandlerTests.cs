using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Addons.UninstallAddon;
using DndOnePlaceManager.Application.Commands.Card;
using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Application.Interfaces;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Addons
{
    public class UninstallAddonCommandHandlerTests : HandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IGameEventLogger> _logger = new();

        private UninstallAddonCommandHandler Handler() => new(Db, _mediator.Object, Mapper, _logger.Object);

        private AddonModel SeedAddon(Guid gameId, string key = "dnd5e")
        {
            var addon = new AddonModel
            {
                Id = Guid.NewGuid(), Name = "DnD 5e", Key = key, Version = "1.0",
                Views = new List<DndOnePlaceManager.Domain.Entities.BattleMap.CardModel>(),
                Templates = new List<DndOnePlaceManager.Domain.Entities.BattleMap.CardModel>(),
                Actions = new List<DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel>(),
                Resources = new List<DndOnePlaceManager.Domain.Entities.Resources.ResourceModel>(),
            };
            Db.Addons.Add(addon);
            Db.SaveChanges();
            return addon;
        }

        [Fact]
        public async Task Handle_GameNotFound_ThrowsResourceNotFoundException()
        {
            var cmd = new UninstallAddonCommand { GameID = Guid.NewGuid(), Player = Player(), AddonId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_AddonNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = Guid.NewGuid() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_ValidUninstall_RemovesAddonAndReturnsInfo()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(addon.Id, result.AddonId);
            Assert.Equal("dnd5e", result.AddonKey);
            Assert.False(Db.Addons.Any(a => a.Id == addon.Id));
        }

        [Fact]
        public async Task Handle_FoundByKey_Succeeds()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id, key: "custom-key");
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonKey = "custom-key" };

            var (response, result) = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.Equal(addon.Id, result.AddonId);
        }

        [Fact]
        public async Task Handle_AddonWithNoChildren_DoesNotSendAnySubCommands()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveCardCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveActionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AddonWithChildren_SendsRemoveCommandForEachChild()
        {
            var game = BuildGame();
            var addon = SeedAddon(game.Id);
            var resource = new DndOnePlaceManager.Domain.Entities.Resources.ResourceModel { Id = Guid.NewGuid(), GameId = game.Id, PlayerId = PlayerId, Name = "r" };
            var view = new DndOnePlaceManager.Domain.Entities.BattleMap.CardModel { Id = Guid.NewGuid(), Name = "v", AdditionalResources = "[]" };
            var action = new DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel { Id = Guid.NewGuid(), Name = "a", Content = "[]", Prefix = "addon" };
            Db.Resources!.Add(resource);
            Db.Cards!.Add(view);
            Db.Actions!.Add(action);
            Db.SaveChanges();
            addon.Resources!.Add(resource);
            addon.Views!.Add(view);
            addon.Actions!.Add(action);
            Db.SaveChanges();

            var cmd = new UninstallAddonCommand { GameID = game.Id, Player = Player(), AddonId = addon.Id };
            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveResourceCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveCardCommand>(), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<RemoveActionCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

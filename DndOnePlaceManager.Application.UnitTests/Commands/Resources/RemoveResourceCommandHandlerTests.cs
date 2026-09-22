using DndOnePlaceManager.Application.Commands.Resources;
using DndOnePlaceManager.Application.Commands.TreeEntry.RemoveTreeEntry;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Entities.Resources;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Resources
{
    public class RemoveResourceCommandHandlerTests : ResourceDataHandlerTestBase
    {
        private readonly Mock<IMediator> _mediator = new();

        private RemoveResourceCommandHandler Handler() => new(Db, Mapper, _mediator.Object, Storage);

        [Fact]
        public async Task Handle_FoundById_RemovesResourceAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Resources.Any(r => r.Id == resource.Id));
        }

        [Fact]
        public async Task Handle_FoundByKey_RemovesResourceAndReturnsOk()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId, key: "bg-music");
            var cmd = new RemoveResourceCommand { Key = "bg-music", GameId = game.Id, Player = Player() };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
            Assert.False(Db.Resources.Any(r => r.Id == resource.Id));
        }

        // Was a known pre-existing bug (flagged during the Part 1 guard-clause work): `image`
        // was dereferenced (image.PlayerId) before any null check, so a not-found resource
        // crashed with NullReferenceException instead of the real ResourceNotFoundException.
        // Fixed as part of the addon install/uninstall triage — now asserts the real behavior.
        [Fact]
        public async Task Handle_ResourceNotFound_ThrowsResourceNotFoundException()
        {
            var game = BuildGame();
            var cmd = new RemoveResourceCommand { ID = Guid.NewGuid(), GameId = game.Id, Player = Player() };

            await Assert.ThrowsAsync<ResourceNotFoundException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_NotOwnerAndNotResourceOwner_ThrowsPermissionException()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var intruder = NonOwnerPlayer();
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = intruder };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
            Assert.True(Db.Resources.Any(r => r.Id == resource.Id));
        }

        [Fact]
        public async Task Handle_NotResourceOwnerButMarkedIsOwner_Succeeds()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = caller };

            var result = await Handler().Handle(cmd, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, result);
        }

        // Unlike UpdateResourceData/DeleteResourceData, this handler's permission check has no
        // Permission.Edit-flag fallback — only literal resource ownership or IsOwner grant access.
        [Fact]
        public async Task Handle_HasEditPermissionFlagButNotOwner_StillThrowsPermissionException()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var caller = new PlayerDTO { Id = Guid.NewGuid(), Name = "Editor", Permission = Permission.Edit };
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = caller };

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_GameIdMismatch_ThrowsWrongArgumentsException()
        {
            var game = BuildGame();
            var otherGameId = Guid.NewGuid();
            var resource = SeedResource(game, PlayerId);
            // Found by ID (bypasses the GameId filter used in the by-key lookup), but the
            // request's GameId doesn't match the resource's actual game.
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = otherGameId, Player = Player() };

            await Assert.ThrowsAsync<WrongArgumentsException>(() => Handler().Handle(cmd, CancellationToken.None));
        }

        [Fact]
        public async Task Handle_RemovesAssociatedTreeEntry_ViaMediator()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var treeEntry = new TreeEntryModel
            {
                Id = Guid.NewGuid(),
                Game = game,
                Name = "bg-music",
                EntryType = nameof(ResourceModel),
                TargetId = resource.Id,
            };
            Db.TreeEntries.Add(treeEntry);
            Db.SaveChanges();

            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = Player() };
            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(
                It.Is<RemoveTreeEntryCommand>(c => c.TreeEntryId == treeEntry.Id && c.TargetId == resource.Id),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NoAssociatedTreeEntries_DoesNotCallMediator()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = Player() };

            await Handler().Handle(cmd, CancellationToken.None);

            _mediator.Verify(m => m.Send(It.IsAny<RemoveTreeEntryCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Regression coverage from the addon install/uninstall triage: unlike RemoveCard/
        // RemoveAction (both go through GenericDeleteHandler, which cleans these up), this
        // hand-rolled handler never removed Properties/Permissions rows referencing the
        // deleted resource — every addon uninstall (which removes many resources at once)
        // was the largest source of that orphaned-row garbage.
        [Fact]
        public async Task Handle_RemovesAssociatedPropertiesAndPermissions()
        {
            var game = BuildGame();
            var resource = SeedResource(game, PlayerId);
            Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), ParentID = resource.Id, Name = "note", Value = "x" });
            Db.Permissions.Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = resource.Id, PlayerID = PlayerId, Permission = Permission.All });
            Db.SaveChanges();

            var cmd = new RemoveResourceCommand { ID = resource.Id, GameId = game.Id, Player = Player() };
            await Handler().Handle(cmd, CancellationToken.None);

            Assert.False(Db.Properties.Any(p => p.ParentID == resource.Id));
            Assert.False(Db.Permissions.Any(p => p.ModelID == resource.Id));
        }
    }
}

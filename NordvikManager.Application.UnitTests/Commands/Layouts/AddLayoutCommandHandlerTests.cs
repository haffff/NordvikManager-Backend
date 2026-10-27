using NordvikManager.Application.Commands.Layouts.AddLayout;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities;
using NordvikManager.Domain.Enums;
using NordvikManager.Domain.Entities.BattleMap;
using Moq;

namespace NordvikManager.Application.UnitTests.Commands.Layouts
{
    public class AddLayoutCommandHandlerTests : HandlerTestBase
    {
        private AddLayoutCommandHandler Handler() => new(Db, Mapper);

        private LayoutModel SeedLayout(NordvikManager.Domain.Entities.BattleMap.GameModel game, string name, bool isDefault = false)
        {
            var layout = new LayoutModel
            {
                Id = Guid.NewGuid(),
                Name = name,
                Value = "{}",
                Default = isDefault,
                GameModelId = game.Id,
                Game = game,
            };
            Db.Layouts.Add(layout);
            Db.SaveChanges();
            return layout;
        }

        [Fact]
        public async Task Handle_AddDefaultLayout_ClearsExistingDefault()
        {
            var game = BuildGame();
            var existing = SeedLayout(game, "A", isDefault: true);

            var (response, id) = await Handler().Handle(new AddLayoutCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new LayoutDTO { Name = "B", Value = "{}", Default = true },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(Db.Layouts.Find(existing.Id)!.Default);
            Assert.True(Db.Layouts.Find(id)!.Default);
            Assert.Single(Db.Layouts.Where(x => x.GameModelId == game.Id && x.Default));
        }

        [Fact]
        public async Task Handle_AddNonDefaultLayout_LeavesExistingDefault()
        {
            var game = BuildGame();
            var existing = SeedLayout(game, "A", isDefault: true);

            var (response, id) = await Handler().Handle(new AddLayoutCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new LayoutDTO { Name = "B", Value = "{}" },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.True(Db.Layouts.Find(existing.Id)!.Default);
            Assert.False(Db.Layouts.Find(id)!.Default);
        }

        private void SetDisallowPlayerLayouts(NordvikManager.Domain.Entities.BattleMap.GameModel game, string value)
        {
            // No client-set Id — PropertyModel.Id is DatabaseGenerated(Identity); an
            // explicit Guid makes EF InMemory treat it as an existing row on SaveChanges.
            Db.Add(new PropertyModel { Name = "disallowPlayerLayouts", Value = value, EntityName = "GameModel", ParentID = game.Id, Game = game });
            Db.SaveChanges();
        }

        [Fact]
        public async Task Handle_DisallowPlayerLayouts_NonGmPlayer_ThrowsPermissionException()
        {
            var game = BuildGame();
            SetDisallowPlayerLayouts(game, "true");
            // Fall through the GM shortcut so the property check runs.
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);

            await Assert.ThrowsAsync<PermissionException>(() => Handler().Handle(new AddLayoutCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new LayoutDTO { Name = "B", Value = "{}" },
            }, CancellationToken.None));

            Assert.Empty(Db.Layouts.Where(x => x.GameModelId == game.Id));
        }

        [Fact]
        public async Task Handle_DisallowPlayerLayouts_GmMaster_StillSucceeds()
        {
            var game = BuildGame();
            game.MasterId = PlayerId;
            Db.SaveChanges();
            SetDisallowPlayerLayouts(game, "true");
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);

            var (response, _) = await Handler().Handle(new AddLayoutCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new LayoutDTO { Name = "B", Value = "{}" },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
        }

        [Fact]
        public async Task Handle_NoDisallowProperty_NonGmPlayer_Succeeds()
        {
            var game = BuildGame();
            PermissionsMock.Setup(p => p.CheckIfHasPermissions(PlayerId, It.IsAny<NordvikManager.Domain.Entities.Interfaces.IEntity>(), Permission.Edit))
                .Returns(false);

            var (response, _) = await Handler().Handle(new AddLayoutCommand
            {
                GameID = game.Id,
                Player = Player(),
                Dto = new LayoutDTO { Name = "B", Value = "{}" },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
        }
    }
}

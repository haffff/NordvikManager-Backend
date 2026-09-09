using DndOnePlaceManager.Application.Commands.Layouts.UpdateLayout;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;

namespace DndOnePlaceManager.Application.UnitTests.Commands.Layouts
{
    public class UpdateLayoutCommandHandlerTests : HandlerTestBase
    {
        private UpdateLayoutCommandHandler Handler() => new(Db, Mapper);

        private LayoutModel SeedLayout(DNDOnePlaceManager.Domain.Entities.BattleMap.GameModel game, string name, bool isDefault = false)
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
        public async Task Handle_SetDefaultOnB_ClearsDefaultOnA()
        {
            var game = BuildGame();
            var a = SeedLayout(game, "A", isDefault: true);
            var b = SeedLayout(game, "B");

            var response = await Handler().Handle(new UpdateLayoutCommand
            {
                Player = Player(),
                Dto = new LayoutDTO { Id = b.Id, GameModelId = game.Id, Default = true },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            Assert.False(Db.Layouts.Find(a.Id)!.Default);
            Assert.True(Db.Layouts.Find(b.Id)!.Default);
            Assert.Single(Db.Layouts.Where(x => x.GameModelId == game.Id && x.Default));
        }

        [Fact]
        public async Task Handle_ReSetCurrentDefault_ReturnsNoChange()
        {
            var game = BuildGame();
            var a = SeedLayout(game, "A", isDefault: true);

            var response = await Handler().Handle(new UpdateLayoutCommand
            {
                Player = Player(),
                Dto = new LayoutDTO { Id = a.Id, GameModelId = game.Id, Default = true },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.NoChange, response);
            Assert.True(Db.Layouts.Find(a.Id)!.Default);
        }

        [Fact]
        public async Task Handle_UpdatesNameAndValue()
        {
            var game = BuildGame();
            var a = SeedLayout(game, "A");

            var response = await Handler().Handle(new UpdateLayoutCommand
            {
                Player = Player(),
                Dto = new LayoutDTO { Id = a.Id, GameModelId = game.Id, Name = "Renamed", Value = "{\"x\":1}" },
            }, CancellationToken.None);

            Assert.Equal(CommandResponse.Ok, response);
            var updated = Db.Layouts.Find(a.Id)!;
            Assert.Equal("Renamed", updated.Name);
            Assert.Equal("{\"x\":1}", updated.Value);
        }
    }
}

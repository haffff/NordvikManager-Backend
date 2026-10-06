using DndOnePlaceManager.Application.Commands.Actions;
using DndOnePlaceManager.Application.Commands.Folder.AddFolder;
using DndOnePlaceManager.Application.Commands.Layouts.AddLayout;
using DndOnePlaceManager.Application.Commands.Map.AddMap;
using DndOnePlaceManager.Application.Commands.Map.UpdateMap;
using DndOnePlaceManager.Application.DataTransferObjects;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Entities;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using MediatR;
using Moq;
using ActionModel = DndOnePlaceManager.Domain.Entities.BattleMap.ActionModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands
{
    // These handlers used to load the whole collection (every map / action / layout of the
    // game) to add or change one item.
    public class CollectionLoadingTests : HandlerTestBase
    {
        private readonly GameModel game;
        private readonly Guid mapId;
        private readonly Guid actionId;
        private readonly Guid defaultLayoutId;

        public CollectionLoadingTests()
        {
            game = BuildGame();
            for (int i = 0; i < 3; i++)
            {
                var map = new MapModel { Id = Guid.NewGuid(), Name = $"Map {i}", Game = game, Elements = new(), Properties = new() };
                Db.Maps.Add(map);
                mapId = map.Id;
                var action = new ActionModel { Id = Guid.NewGuid(), Name = $"Action {i}", Content = "[]", Prefix = "core", Game = game };
                Db.Actions.Add(action);
                actionId = action.Id;
                var layout = new LayoutModel { Id = Guid.NewGuid(), Name = $"Layout {i}", Value = "{\"big\":\"json\"}", Default = i == 0, GameModelId = game.Id, Game = game };
                Db.Layouts.Add(layout);
                if (i == 0) defaultLayoutId = layout.Id;
                Db.Properties.Add(new PropertyModel { Id = Guid.NewGuid(), Name = $"setting{i}", Value = "v", ParentID = game.Id, EntityName = "GameModel", Game = game });
            }
            Db.SaveChanges();
            Db.ChangeTracker.Clear();
        }

        private IEnumerable<Guid> Tracked<T>() where T : class, DndOnePlaceManager.Domain.Entities.Interfaces.IEntity =>
            Db.ChangeTracker.Entries<T>().Select(e => e.Entity.Id).OrderBy(x => x);

        [Fact]
        public async Task AddMap_LoadsNoOtherMaps()
        {
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<AddTreeEntryCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CommandResponse.Ok, new List<TreeEntryDto>()));

            var (_, id) = await new AddMapCommandHandler(Db, Mapper, mediator.Object)
                .Handle(new AddMapCommand { GameID = game.Id, Player = Player() }, CancellationToken.None);

            Assert.Equal(new[] { id }, Tracked<MapModel>());
            using var check = SeedContext();
            Assert.Equal(4, check.Maps.Count(m => m.Game.Id == game.Id));
        }

        [Fact]
        public async Task UpdateMap_LoadsOnlyThatMap()
        {
            await new UpdateMapCommandHandler(Db, Mapper).Handle(new UpdateMapCommand
            {
                GameId = game.Id, Player = Player(), Map = new MapDTO { Id = mapId, Name = "Renamed" },
            }, CancellationToken.None);

            Assert.Equal(new[] { mapId }, Tracked<MapModel>());
            using var check = SeedContext();
            Assert.Equal("Renamed", check.Maps.Find(mapId)!.Name);
        }

        [Fact]
        public async Task AddAction_LoadsNoOtherActions()
        {
            var (_, id) = await new AddActionCommandHandler(Db, Mapper).Handle(new AddActionCommand
            {
                GameId = game.Id, Player = Player(), Action = new ActionDto { Name = "New", Content = "[]", Prefix = "core" },
            }, CancellationToken.None);

            Assert.Equal(new[] { id }, Tracked<ActionModel>());
            using var check = SeedContext();
            Assert.Equal(4, check.Actions.Count(a => a.Game.Id == game.Id));
        }

        [Fact]
        public async Task UpdateAction_LoadsOnlyThatAction()
        {
            await new UpdateActionCommandHandler(Db, Mapper).Handle(new UpdateActionCommand
            {
                GameId = game.Id, Player = Player(), Action = new ActionDto { Id = actionId, Name = "Renamed", Content = "[]", Prefix = "core" },
            }, CancellationToken.None);

            Assert.Equal(new[] { actionId }, Tracked<ActionModel>());
            using var check = SeedContext();
            Assert.Equal("Renamed", check.Actions.Find(actionId)!.Name);
        }

        [Fact]
        public async Task AddLayout_NonDefault_LoadsNoOtherLayoutsOrProperties()
        {
            var (_, id) = await new AddLayoutCommandHandler(Db, Mapper).Handle(new AddLayoutCommand
            {
                GameID = game.Id, Player = Player(), Dto = new LayoutDTO { Name = "Mine", Value = "{}" },
            }, CancellationToken.None);

            Assert.Equal(new[] { id }, Tracked<LayoutModel>());
            Assert.Empty(Db.ChangeTracker.Entries<PropertyModel>());
        }

        [Fact]
        public async Task AddLayout_Default_LoadsOnlyTheOldDefaultAndClearsIt()
        {
            var (_, id) = await new AddLayoutCommandHandler(Db, Mapper).Handle(new AddLayoutCommand
            {
                GameID = game.Id, Player = Player(), Dto = new LayoutDTO { Name = "New default", Value = "{}", Default = true },
            }, CancellationToken.None);

            Assert.Equal(new[] { id, defaultLayoutId }.OrderBy(x => x), Tracked<LayoutModel>());
            using var check = SeedContext();
            Assert.False(check.Layouts.Find(defaultLayoutId)!.Default);
            Assert.True(check.Layouts.Find(id)!.Default);
        }
    }
}

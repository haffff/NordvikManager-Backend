using DndOnePlaceManager.Application.Commands.TurnOrder;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Exceptions;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using Microsoft.EntityFrameworkCore;
using ElementDetailModel = DndOnePlaceManager.Domain.Entities.BattleMap.ElementDetailModel;

namespace DndOnePlaceManager.Application.UnitTests.Commands.TurnOrder
{
    public class TurnOrderCommandHandlerTests : SqliteHandlerTestBase
    {
        private readonly Guid gameId;
        private readonly Guid mapId;
        private readonly Guid player = Guid.NewGuid();   // Read on the map, Control on the hero
        private readonly Guid hero;
        private readonly Guid goblin;
        private readonly Guid orc;

        public TurnOrderCommandHandlerTests()
        {
            using var seed = SeedContext();
            var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", MasterId = PlayerId, SystemPlayerId = Guid.NewGuid(), Players = new List<PlayerModel>() };
            var map = new MapModel { Id = Guid.NewGuid(), Name = "Crypt", Game = game };
            ElementModel Token(string name) => new()
            {
                Id = Guid.NewGuid(),
                Map = map,
                Selectable = true,
                Details = new List<ElementDetailModel> { new() { Key = "name", Value = name, Type = "String" }, new() { Key = "isToken", Value = "True", Type = "Boolean" } },
            };
            var heroToken = Token("token Hero");
            var goblinToken = Token("token Goblin");
            var orcToken = Token("token Orc");
            seed.Games.Add(game);
            seed.Maps.Add(map);
            seed.Elements.AddRange(heroToken, goblinToken, orcToken);
            seed.Permissions.AddRange(
                new PermissionModel { ModelID = map.Id, PlayerID = PlayerId, Permission = Permission.All },
                new PermissionModel { ModelID = map.Id, PlayerID = player, Permission = Permission.Read },
                new PermissionModel { ModelID = heroToken.Id, PlayerID = player, Permission = Permission.Read | Permission.Execute | Permission.Control });
            seed.SaveChanges();
            gameId = game.Id;
            mapId = map.Id;
            hero = heroToken.Id;
            goblin = goblinToken.Id;
            orc = orcToken.Id;
        }

        private static PlayerDTO As(Guid id) => new() { Id = id, Name = "p" };
        private PlayerDTO Gm => As(PlayerId);

        private Task<(CommandResponse, TurnOrderNotice?)> Run(TurnOrderCommand cmd, PlayerDTO? by = null)
        {
            cmd.GameId = gameId;
            cmd.MapId = mapId;
            cmd.Player = by ?? Gm;
            return new TurnOrderCommandHandler(Db, Mapper).Handle(cmd, CancellationToken.None);
        }

        private Task<TurnOrderDto?> State(PlayerDTO? by = null) =>
            new GetTurnOrderCommandHandler(Db, Mapper).Handle(new GetTurnOrderCommand { GameId = gameId, MapId = mapId, Player = by ?? Gm }, CancellationToken.None);

        private Task Add(TurnOrderEntryInput first, params TurnOrderEntryInput[] rest) =>
            Run(new TurnOrderCommand { Operation = TurnOrderOperation.Add, Entries = rest.Prepend(first).ToList() });

        private async Task<List<string>> Names() => (await State())!.Entries.Select(e => e.Name).ToList();

        private async Task<string?> Current()
        {
            var state = (await State())!;
            return state.Entries.FirstOrDefault(e => e.Id == state.CurrentEntryId)?.Name;
        }

        // ── Adding ───────────────────────────────────────────────────────────

        [Fact]
        public async Task Add_FirstEntries_StartRoundOneWithTheFirstOneCurrent()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { Name = "Lair action", Initiative = 20 });

            var state = (await State())!;
            Assert.Equal(1, state.Round);
            Assert.Equal(new[] { "token Hero", "Lair action" }, state.Entries.Select(e => e.Name));
            Assert.Equal("token Hero", await Current());
        }

        [Fact]
        public async Task Add_ATokenAlreadyInTheOrder_IsSkipped()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero });
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = goblin });

            Assert.Equal(new[] { "token Hero", "token Goblin" }, await Names());
        }

        [Fact]
        public async Task Add_ATokenOfAnotherMap_IsRefused()
        {
            await Assert.ThrowsAsync<WrongArgumentsException>(() => Add(new TurnOrderEntryInput { ElementId = Guid.NewGuid() }));
        }

        [Fact]
        public async Task Add_ByAPlayerWithoutEditOnTheMap_IsRefused()
        {
            await Assert.ThrowsAsync<PermissionException>(() =>
                Run(new TurnOrderCommand { Operation = TurnOrderOperation.Add, Entries = new() { new() { Name = "Sneaky" } } }, As(player)));
        }

        // ── Sorting and order ────────────────────────────────────────────────

        [Fact]
        public async Task Sort_HighestInitiativeFirst_TiesKeepTheirPlace_UnsetLast()
        {
            await Add(new TurnOrderEntryInput { Name = "A", Initiative = 10 }, new TurnOrderEntryInput { Name = "B" }, new TurnOrderEntryInput { Name = "C", Initiative = 15 }, new TurnOrderEntryInput { Name = "D", Initiative = 10 });

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Sort });

            Assert.Equal(new[] { "C", "A", "D", "B" }, await Names());
        }

        [Fact]
        public async Task Reorder_SetsTheGivenOrder()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" }, new TurnOrderEntryInput { Name = "C" });
            var ids = (await State())!.Entries.Select(e => e.Id).ToList();

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Reorder, EntryIds = new() { ids[2], ids[0], ids[1] } });

            Assert.Equal(new[] { "C", "A", "B" }, await Names());
        }

        [Fact]
        public async Task Update_SetsInitiativeAndCanSortRightAway()
        {
            await Add(new TurnOrderEntryInput { Name = "A", Initiative = 5 }, new TurnOrderEntryInput { Name = "B", Initiative = 3 });
            var b = (await State())!.Entries[1].Id;

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Update, EntryId = b, Initiative = 18, SortAfter = true });

            Assert.Equal(new[] { "B", "A" }, await Names());
            Assert.Equal(18, (await State())!.Entries[0].Initiative);
        }

        [Fact]
        public async Task Update_ByToken_FindsItsEntry()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = goblin });

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Update, ElementId = goblin, Initiative = 9 });

            Assert.Equal(9, (await State())!.Entries.Single(e => e.ElementId == goblin).Initiative);
        }

        // ── Turns and rounds ─────────────────────────────────────────────────

        [Fact]
        public async Task Advance_PastTheLastEntry_StartsTheNextRound()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" });

            var (_, first) = await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });
            var (_, second) = await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });

            Assert.Equal("A", await Current());
            Assert.Equal(2, (await State())!.Round);
            Assert.True(first!.TurnChanged);
            Assert.Equal(2, second!.Round);
        }

        [Fact]
        public async Task Advance_BackPastTheFirstEntry_GoesToThePreviousRoundButNotBelowOne()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" });
            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });
            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance }); // round 2, A

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance, Direction = -1 }); // round 1, B
            Assert.Equal(("B", 1), (await Current(), (await State())!.Round));

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance, Direction = -1 }); // A
            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance, Direction = -1 }); // stays round 1
            Assert.Equal(1, (await State())!.Round);
        }

        [Fact]
        public async Task Advance_ToAnEntry_JumpsToIt()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" }, new TurnOrderEntryInput { Name = "C" });
            var c = (await State())!.Entries[2].Id;

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance, EntryId = c });

            Assert.Equal("C", await Current());
            Assert.Equal(1, (await State())!.Round);
        }

        [Fact]
        public async Task Remove_TheCurrentEntry_PassesTheTurnOn()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" });
            var a = (await State())!.Entries[0].Id;

            var (_, notice) = await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Remove, EntryIds = new() { a } });

            Assert.Equal(new[] { "B" }, await Names());
            Assert.Equal("B", await Current());
            Assert.True(notice!.TurnChanged);
        }

        [Fact]
        public async Task Reset_KeepsEntriesAndRestarts_OrClearsThem()
        {
            await Add(new TurnOrderEntryInput { Name = "A" }, new TurnOrderEntryInput { Name = "B" });
            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });
            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Reset });
            Assert.Equal(("A", 1), (await Current(), (await State())!.Round));

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Reset, Clear = true });
            Assert.Empty((await State())!.Entries);
            Assert.Null((await State())!.CurrentEntryId);
        }

        // ── Ending your own turn ─────────────────────────────────────────────

        [Fact]
        public async Task EndTurn_ByThePlayerControllingTheCurrentToken_PassesTheTurn()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = goblin });

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.EndTurn }, As(player));

            Assert.Equal("token Goblin", await Current());
        }

        [Fact]
        public async Task State_SaysWhetherThisPlayerMayEndTheCurrentTurn()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = goblin });

            Assert.True((await State(As(player)))!.CanEndTurn);   // their hero's turn
            Assert.True((await State())!.CanEndTurn);             // the GM always may

            await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });
            Assert.False((await State(As(player)))!.CanEndTurn);  // the goblin's turn
        }

        [Fact]
        public async Task EndTurn_OnSomeoneElsesTurn_IsRefused()
        {
            await Add(new TurnOrderEntryInput { ElementId = goblin }, new TurnOrderEntryInput { ElementId = hero });

            await Assert.ThrowsAsync<PermissionException>(() => Run(new TurnOrderCommand { Operation = TurnOrderOperation.EndTurn }, As(player)));
        }

        // ── What players see ─────────────────────────────────────────────────

        [Fact]
        public async Task State_ForAPlayer_LeavesOutHiddenEntries_AndHidesWhoseTurnItIsWhenHidden()
        {
            await Add(new TurnOrderEntryInput { ElementId = orc, Hidden = true }, new TurnOrderEntryInput { ElementId = hero });

            var playerView = (await State(As(player)))!;
            Assert.Equal(new[] { "token Hero" }, playerView.Entries.Select(e => e.Name));
            Assert.Null(playerView.CurrentEntryId);
            Assert.True(playerView.CurrentHidden);

            var gmView = (await State())!;
            Assert.Equal(2, gmView.Entries.Count);
            Assert.True(gmView.Entries[0].Hidden);
        }

        [Fact]
        public async Task Notice_OnAHiddenEntrysTurn_DoesNotGiveItAway()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = orc, Hidden = true });

            var (_, notice) = await Run(new TurnOrderCommand { Operation = TurnOrderOperation.Advance });

            Assert.Null(notice!.CurrentEntryId);
            Assert.Null(notice.ElementId);
            Assert.True(notice.TurnChanged);
        }

        [Fact]
        public async Task State_OfAMapWithoutATurnOrder_IsEmpty_AndNeedsReadOnTheMap()
        {
            var state = (await State(As(player)))!;
            Assert.Empty(state.Entries);
            Assert.Equal(1, state.Round);

            await Assert.ThrowsAsync<PermissionException>(() => State(As(Guid.NewGuid())));
        }

        [Fact]
        public async Task RemovingAToken_TakesItOutOfTheOrder()
        {
            await Add(new TurnOrderEntryInput { ElementId = hero }, new TurnOrderEntryInput { ElementId = goblin });

            var changed = await TurnOrderCleanup.RemoveElementAsync(Db, hero);

            Assert.Equal(mapId, changed);
            Assert.Equal(new[] { "token Goblin" }, await Names());
            Assert.Equal("token Goblin", await Current());
        }
    }
}

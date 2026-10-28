using NordvikManager.Application.Commands.Security.SetViewPermission;
using NordvikManager.Application.Exceptions;
using NordvikManager.Application.Services;
using NordvikManager.Domain.Entities.Interfaces;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace NordvikManager.Application.UnitTests.Commands.Security
{
    // The SetPermission step lets an action share something for viewing (a lore card with
    // the players) or let a player move a token. Nothing more: never Edit (a card's code
    // runs with the rights of whoever opens it), never on actions, templates or the game,
    // and only by someone who already has that right. Uses the real permission rules.
    public class SetViewPermissionCommandHandlerTests : HandlerTestBase
    {
        private readonly PermissionsService _perms;
        private readonly GameModel _game;
        private readonly Guid _gm, _alice = Guid.NewGuid(), _bob = Guid.NewGuid();
        private readonly CardModel _lore;
        private readonly ElementModel _token;
        private readonly MapModel _map;

        public SetViewPermissionCommandHandlerTests()
        {
            _perms = new PermissionsService(new ServiceCollection().AddSingleton<IDbContext>(Db).BuildServiceProvider());
            _gm = PlayerId;
            _game = new GameModel
            {
                Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid(), MasterId = _gm,
                Maps = new List<MapModel>(),
                Players = new List<PlayerModel>
                {
                    new() { Id = _gm, Name = "GM" },
                    new() { Id = _alice, Name = "Alice" },
                    new() { Id = _bob, Name = "Bob" },
                },
            };
            Db.Games.Add(_game);
            _map = new MapModel { Id = Guid.NewGuid(), Name = "Map", Game = _game };
            Db.Maps.Add(_map);
            _token = new ElementModel { Id = Guid.NewGuid(), Map = _map };
            Db.Elements.Add(_token);
            _lore = new CardModel { Id = Guid.NewGuid(), Name = "The Old Mill", GameId = _game.Id, Properties = new() };
            Db.Cards.Add(_lore);
            Db.SaveChanges();
            foreach (var entity in new IEntity[] { _lore, _token, _map })
                _perms.SetPermissions(_gm, entity, Permission.All);
        }

        private SetViewPermissionCommandHandler Handler() => new(Db, Mapper, _perms);

        private Task<SetViewPermissionResult> Run(Guid entityId, string? player, Permission permission = Permission.Read, bool revoke = false, Guid? caller = null) =>
            Handler().Handle(new SetViewPermissionCommand
            {
                GameId = _game.Id,
                CallerId = caller ?? _gm,
                EntityId = entityId,
                Player = player,
                Permission = permission,
                Revoke = revoke,
            }, CancellationToken.None);

        private bool Can(Guid player, IEntity entity, Permission permission) => _perms.CheckIfHasPermissions(player, entity, permission);

        // ── what it does ─────────────────────────────────────────────────────

        [Fact]
        public async Task Gm_SharesACardWithAPlayer()
        {
            var result = await Run(_lore.Id, _alice.ToString());

            Assert.True(Can(_alice, _lore, Permission.Read));
            Assert.False(Can(_bob, _lore, Permission.Read));
            Assert.Equal(new[] { _alice }, result.GainedRead);
            Assert.Equal("card", result.EntityKind);
        }

        [Fact]
        public async Task Gm_SharesACardWithAPlayer_ByName()
        {
            await Run(_lore.Id, "alice");

            Assert.True(Can(_alice, _lore, Permission.Read));
        }

        [Fact]
        public async Task Gm_SharesACardWithEveryone()
        {
            var result = await Run(_lore.Id, null);

            Assert.True(Can(_alice, _lore, Permission.Read));
            Assert.True(Can(_bob, _lore, Permission.Read));
            Assert.Equal(new[] { _alice, _bob }.OrderBy(x => x), result.GainedRead.OrderBy(x => x));
        }

        [Fact]
        public async Task SharingAgain_GainsNobody()
        {
            await Run(_lore.Id, _alice.ToString());

            var result = await Run(_lore.Id, _alice.ToString());

            Assert.Empty(result.GainedRead);
        }

        [Fact]
        public async Task Gm_Unshares()
        {
            await Run(_lore.Id, _alice.ToString());

            var result = await Run(_lore.Id, _alice.ToString(), revoke: true);

            Assert.False(Can(_alice, _lore, Permission.Read));
            Assert.Equal(new[] { _alice }, result.LostRead);
        }

        [Fact]
        public async Task Gm_UnsharesFromEveryone()
        {
            await Run(_lore.Id, null);

            var result = await Run(_lore.Id, null, revoke: true);

            Assert.False(Can(_bob, _lore, Permission.Read));
            Assert.Contains(_bob, result.LostRead);
            Assert.True(Can(_gm, _lore, Permission.All));
        }

        [Fact]
        public async Task Gm_LetsAPlayerMoveAToken()
        {
            await Run(_token.Id, _alice.ToString(), Permission.Control);

            Assert.True(Can(_alice, _token, Permission.Control));
            Assert.False(Can(_alice, _token, Permission.Edit));
        }

        [Fact]
        public async Task Gm_SharesAMap()
        {
            var result = await Run(_map.Id, _alice.ToString());

            Assert.True(Can(_alice, _map, Permission.Read));
            Assert.Equal("map", result.EntityKind);
        }

        // ── what it never does ───────────────────────────────────────────────

        [Theory]
        [InlineData(Permission.Edit)]
        [InlineData(Permission.Remove)]
        [InlineData(Permission.All)]
        [InlineData(Permission.Execute)]
        [InlineData(Permission.Read | Permission.Edit)]
        [InlineData(Permission.None)]
        public async Task OnlyReadOrControl(Permission permission)
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, _alice.ToString(), permission));

            Assert.False(Can(_alice, _lore, Permission.Read));
        }

        [Fact]
        public async Task NotForEveryone_Control()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_token.Id, null, Permission.Control));
        }

        [Fact]
        public async Task NotATemplate()
        {
            var template = new CardModel { Id = Guid.NewGuid(), Name = "T", GameId = _game.Id, IsTemplate = true, Properties = new() };
            Db.Cards.Add(template);
            Db.SaveChanges();

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(template.Id, _alice.ToString()));
        }

        [Fact]
        public async Task NotAView()
        {
            var view = new CardModel { Id = Guid.NewGuid(), Name = "V", GameId = _game.Id, IsCustomUi = true, Properties = new() };
            Db.Cards.Add(view);
            Db.SaveChanges();

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(view.Id, _alice.ToString()));
        }

        [Fact]
        public async Task NotAnAction()
        {
            var action = new ActionModel { Id = Guid.NewGuid(), Name = "a", Content = "[]", Prefix = "x", Game = _game };
            Db.Actions.Add(action);
            Db.SaveChanges();

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(action.Id, _alice.ToString(), Permission.Read));
        }

        [Fact]
        public async Task NotTheGame()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_game.Id, _alice.ToString()));
        }

        [Fact]
        public async Task NotAnotherGamesCard()
        {
            var other = new GameModel { Id = Guid.NewGuid(), Name = "Other", SystemPlayerId = Guid.NewGuid() };
            Db.Games.Add(other);
            var card = new CardModel { Id = Guid.NewGuid(), Name = "C", GameId = other.Id, Properties = new() };
            Db.Cards.Add(card);
            Db.SaveChanges();
            _perms.SetPermissions(_gm, card, Permission.All);

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(card.Id, _alice.ToString()));
        }

        [Fact]
        public async Task NotWithoutAPlayerWhoTriggeredIt()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Handler().Handle(new SetViewPermissionCommand
            {
                GameId = _game.Id, CallerId = null, EntityId = _lore.Id, Player = _alice.ToString(), Permission = Permission.Read,
            }, CancellationToken.None));
        }

        [Fact]
        public async Task NotByAPlayerWhoCanOnlyReadIt()
        {
            _perms.SetPermissions(_alice, _lore, Permission.Read);

            await Assert.ThrowsAsync<PermissionException>(() => Run(_lore.Id, _bob.ToString(), caller: _alice));

            Assert.False(Can(_bob, _lore, Permission.Read));
        }

        [Fact]
        public async Task NotARightTheCallerDoesntHave()
        {
            _perms.SetPermissions(_alice, _token, Permission.Read | Permission.Edit); // no Control

            await Assert.ThrowsAsync<PermissionException>(() => Run(_token.Id, _bob.ToString(), Permission.Control, caller: _alice));
        }

        [Fact]
        public async Task NeverTheGmsOrTheSystemsRow()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, _gm.ToString(), revoke: true));
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, _game.SystemPlayerId.ToString(), revoke: true));

            Assert.True(Can(_gm, _lore, Permission.All));
        }

        [Fact]
        public async Task NeverLowersSomeoneWhoCanEditIt()
        {
            _perms.SetPermissions(_alice, _lore, Permission.All);

            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, _alice.ToString(), revoke: true));

            Assert.True(Can(_alice, _lore, Permission.Edit));
        }

        [Fact]
        public async Task OnlyPlayersOfThisGame()
        {
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, Guid.NewGuid().ToString()));
            await Assert.ThrowsAsync<ActionProcessException>(() => Run(_lore.Id, "nobody"));
        }
    }
}

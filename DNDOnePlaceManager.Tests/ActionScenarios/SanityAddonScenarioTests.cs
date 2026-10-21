using DndOnePlaceManager.Application;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Application.Services.Dice;
using DndOnePlaceManager.Domain.Entities.BattleMap;
using DndOnePlaceManager.Domain.Entities.Security;
using DndOnePlaceManager.Domain.Enums;
using DndOnePlaceManager.Infrastructure;
using DndOnePlaceManager.Infrastructure.Interfaces;
using DNDOnePlaceManager.Data.Contexts;
using DNDOnePlaceManager.Domain.Entities.BattleMap;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.ActionSteps;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using DNDOnePlaceManager.WebSockets;
using DNDOnePlaceManager.WebSockets.Handlers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using CardModel = DndOnePlaceManager.Domain.Entities.BattleMap.CardModel;

namespace DNDOnePlaceManager.Tests.ActionScenarios
{
    // Runs the example actions of ActionScenarios/sanity (an installable addon) on a real
    // server: the real steps, handlers and mediator over an in-memory SQLite game. Only the
    // players' browsers are stood in for. The scenarios are the ones a GM asked for: up to N
    // targets then an action (Bless), Fireball confirmed by the GM, a random monster from
    // marked cards, and random damage to every token on a map.
    public class SanityAddonScenarioTests : IDisposable
    {
        // Every die shows 3 (or its highest face when that is lower).
        private sealed class FixedDice : IDiceRandomSource
        {
            public int Roll(int sides) => Math.Min(3, sides);
        }

        private readonly SqliteConnection keepAlive;
        private readonly ServiceProvider services;
        private readonly GameLobby lobby;
        private readonly List<(PlayerDTO To, WebSocketCommand Command)> sent = new();
        private string gmAnswer = "yes";

        private readonly Guid gameId = Guid.NewGuid();
        private readonly PlayerDTO gm;
        private readonly PlayerDTO player;
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid goblinCard = Guid.NewGuid(), orcCard = Guid.NewGuid();
        private readonly Guid goblin = Guid.NewGuid(), rat = Guid.NewGuid(), farOrc = Guid.NewGuid();

        private readonly Xunit.Abstractions.ITestOutputHelper output;

        public SanityAddonScenarioTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            this.output = output;
            var connectionString = $"Data Source=sanity{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            keepAlive = new SqliteConnection(connectionString);
            keepAlive.Open();

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:DBData"] = connectionString,
            }).Build();

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<IConfiguration>(config);
            InfrastructureLayerModule.Register(collection, config);
            ApplicationLayerModule.Register(collection, config);
            collection.AddSingleton<IDiceRandomSource, FixedDice>();
            collection.AddScoped<DndOnePlaceManager.Application.Interfaces.IGameEventLogger, LobbyGameEventLogger>();
            collection.AddSingleton(new Mock<IPlaybackService>().Object);
            // Internal to the API assembly, like its registration in Startup.
            collection.AddScoped(typeof(IActionProcessingService), typeof(IActionProcessingService).Assembly
                .GetType("DNDOnePlaceManager.Services.Implementations.ActionProcessingService")!);
            foreach (var type in typeof(GameLobby).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsClass))
            {
                if (typeof(IWebSocketHandler).IsAssignableFrom(type)) collection.AddScoped(typeof(IWebSocketHandler), type);
                if (typeof(IActionStepDefinition).IsAssignableFrom(type)) collection.AddSingleton(typeof(IActionStepDefinition), type);
            }
            services = collection.BuildServiceProvider();
            ApplicationLayerModule.AfterBuild(services);

            gm = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
            player = new PlayerDTO { Id = Guid.NewGuid(), Name = "Wizard" };
            var systemId = Guid.NewGuid();
            Seed(systemId);

            lobby = new GameLobby(services.GetRequiredService<IServiceScopeFactory>())
            {
                GameId = gameId,
                SystemPlayer = new PlayerDTO { Id = systemId, Name = "System", System = true },
            };
            lobby.ConnectedPlayers[gm] = new() { Connection(gm) };
            lobby.ConnectedPlayers[player] = new() { Connection(player) };
        }

        public void Dispose()
        {
            // What happened, for a failing scenario: the event log and every message sent.
            foreach (var e in lobby.EventLog.GetEntries())
                output.WriteLine($"[{e.Level}] {e.Category}: {e.Message}");
            lock (sent)
                foreach (var (to, command) in sent)
                    output.WriteLine($"-> {to.Name}: {command.Command} {command.Data?.ToString(Newtonsoft.Json.Formatting.None)}");
            services.Dispose();
            keepAlive.Dispose();
        }

        // ── The game: a 50 px grid map with three tokens, and the addon's actions ──

        private void Seed(Guid systemId)
        {
            using var scope = services.CreateScope();
            var db = (DndOneContext)scope.ServiceProvider.GetRequiredService<IDbContext>();
            db.Database.EnsureCreated();

            var game = new GameModel
            {
                Id = gameId, Name = "Sanity", MasterId = gm.Id!.Value, SystemPlayerId = systemId,
                Players = new List<PlayerModel>
                {
                    new() { Id = gm.Id.Value, Name = gm.Name },
                    new() { Id = player.Id!.Value, Name = player.Name },
                    new() { Id = systemId, Name = "System", System = true },
                },
                Cards = new List<CardModel>
                {
                    new() { Id = goblinCard, Name = "Goblin", Description = "" },
                    new() { Id = orcCard, Name = "Orc", Description = "" },
                },
                Maps = new List<MapModel>
                {
                    new()
                    {
                        Id = mapId, Name = "Cave", GridSize = 50,
                        Elements = new List<ElementModel>
                        {
                            // Centres (125,125) and (175,125): in a Fireball at (150,125). The orc is far away.
                            Token(goblin, "Goblin", 100, 100, goblinCard),
                            Token(rat, "Rat", 150, 100, cardId: null),
                            Token(farOrc, "Far orc", 1000, 1000, orcCard),
                        },
                    },
                },
            };
            db.Add(game);
            // As the app grants them: the GM and the system own everything; the player may see the game.
            foreach (var entity in new[] { gameId, mapId, goblinCard, orcCard, goblin, rat, farOrc })
                db.Set<PermissionModel>().AddRange(
                    new PermissionModel { Id = Guid.NewGuid(), ModelID = entity, PlayerID = gm.Id.Value, Permission = Permission.All },
                    new PermissionModel { Id = Guid.NewGuid(), ModelID = entity, PlayerID = systemId, Permission = Permission.All });
            db.Set<PermissionModel>().Add(new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = player.Id.Value, Permission = Permission.Read });
            // Linked to their owner as the app links them (the ParentID alone isn't the relation).
            var card = game.Cards.ToDictionary(c => c.Id);
            var element = game.Maps.Single().Elements!.ToDictionary(e => e.Id);
            db.Set<PropertyModel>().AddRange(
                Property("CardModel", goblinCard, "hp", "20", c => c.Card = card[goblinCard]),
                Property("CardModel", orcCard, "hp", "30", c => c.Card = card[orcCard]),
                Property("ElementModel", rat, "bar1_value", "7", c => c.Element = element[rat]));

            var folder = Path.Combine(AppContext.BaseDirectory, "ActionScenarios", "sanity", "Actions");
            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                db.Add(new ActionModel
                {
                    Id = Guid.NewGuid(), Game = game, Prefix = "sanity",
                    Name = json.Value<string>("name")!,
                    Hook = (Hook)json.Value<int>("hook"),
                    IsEnabled = json.Value<int>("hook") != 0,
                    Content = json["content"]!.ToString(),
                });
            }
            db.SaveChanges();
        }

        private ElementModel Token(Guid id, string name, int left, int top, Guid? cardId)
        {
            var obj = new JObject
            {
                ["type"] = "image", ["isToken"] = true, ["name"] = name,
                ["left"] = left, ["top"] = top, ["width"] = 50, ["height"] = 50, ["scaleX"] = 1, ["scaleY"] = 1,
                ["originX"] = "left", ["originY"] = "top",
            };
            if (cardId != null)
            {
                obj["cardId"] = cardId.ToString();
                obj["tokenData"] = new JObject { ["cardId"] = cardId.ToString() };
            }
            return services.GetRequiredService<DndOnePlaceManager.Application.Mapping.IMapper>().Map<ElementModel>(new ElementDTO { Id = id, Object = obj.ToString() });
        }

        private static PropertyModel Property(string entity, Guid parent, string name, string value, Action<PropertyModel> link)
        {
            var property = new PropertyModel { Id = Guid.NewGuid(), EntityName = entity, ParentID = parent, Name = name, Value = value };
            link(property);
            return property;
        }

        // A browser: records what it is sent and answers the GM's input requests.
        private IPlayerConnection Connection(PlayerDTO who)
        {
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(message =>
                {
                    if (message is not WebSocketCommand command) return;
                    lock (sent) sent.Add((who, command));
                    if (command.Command == "request_input" && command.InputToken is Guid token)
                        _ = Task.Run(() =>
                        {
                            if (lobby.ActionProcessingService.InputHandler.TryRemove(token, out var answer))
                                answer.TrySetResult(new WebSocketCommand { Data = gmAnswer == null ? null : new JValue(gmAnswer) });
                        });
                })
                .ReturnsAsync(true);
            return connection.Object;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // As a client's FireAction / menu item / map tool would run it.
        private Task Run(string action, PlayerDTO by, object? args = null)
        {
            var data = args == null ? new JObject() : JObject.FromObject(args);
            return lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = by, Data = data, Command = new WebSocketCommand { Command = "execute_action" } },
                data.ToObject<Dictionary<string, object>>());
        }

        private string? PropertyValue(Guid parent, string name)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
            return db.Properties.Where(p => p.ParentID == parent && p.Name == name).Select(p => p.Value).FirstOrDefault();
        }

        private List<string> Chat(PlayerDTO to) =>
            sent.Where(x => x.To == to && x.Command.Command.StartsWith("chat"))
                .Select(x => x.Command.Data?.ToString() ?? "").ToList();

        private void NoErrors()
        {
            var errors = lobby.EventLog.GetEntries().Where(e => e.Level == "Error").Select(e => e.Message).ToList();
            Assert.True(errors.Count == 0, string.Join(" | ", errors));
        }

        private static object At(double x, double y) => new { x, y };

        // ── Scenarios ────────────────────────────────────────────────────────────

        private List<string> Added(PlayerDTO to) =>
            sent.Where(x => x.To == to && x.Command.Command.EndsWith("_add") && !x.Command.Command.StartsWith("property"))
                .Select(x => x.Command.Data!["Name"]!.ToString()).ToList();

        [Fact]
        public async Task Setup_APlayerGetsThePlayerToolsOnly_AndOnlyThemselves()
        {
            await Run("sanity/setup", player);

            NoErrors();
            Assert.Equal(new[] { "sanity_target", "sanity_fireball", "sanity_menu", "sanity_bless", "sanity_clear" }, Added(player));
            Assert.Empty(Added(gm));
        }

        [Fact]
        public async Task Setup_TheGmAlsoGetsTheGmTools()
        {
            await Run("sanity/setup", gm);

            NoErrors();
            Assert.Contains("sanity_damage_all", Added(gm));
            Assert.Contains("sanity_random_enemy", Added(gm));
            Assert.Empty(Added(player));
        }

        [Fact]
        public async Task GmActions_RefusePlayers()
        {
            await Run("sanity/damage_all", player, new { mapId = mapId.ToString(), dice = "1d6" });

            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
        }

        [Fact]
        public async Task Targets_UpToThree_ThenBless()
        {
            var fourth = Guid.NewGuid();
            foreach (var id in new[] { goblin, rat, farOrc, fourth })
                await Run("sanity/toggle_target", player, new { elementId = id.ToString(), maxTargets = "3" });

            NoErrors();
            var marked = JArray.Parse(PropertyValue(gameId, $"sanity_targets_{player.Id}")!);
            Assert.Equal(new[] { goblin, rat, farOrc }.Select(x => x.ToString()), marked.Select(x => x.ToString()));
            Assert.Contains(Chat(player), m => m.Contains("Already 3 targets"));

            await Run("sanity/bless", player);

            NoErrors();
            Assert.Equal("true", PropertyValue(goblin, "sanity_blessed"));
            Assert.Equal("true", PropertyValue(rat, "sanity_blessed"));
            Assert.Equal("true", PropertyValue(farOrc, "sanity_blessed"));
            Assert.Contains(Chat(gm), m => m.Contains("Bless") && m.Contains("Goblin, Rat, Far orc"));
            Assert.Equal("[]", PropertyValue(gameId, $"sanity_targets_{player.Id}"));
        }

        [Fact]
        public async Task Targets_ClickingAMarkedOneUnmarksIt()
        {
            await Run("sanity/toggle_target", player, new { elementId = goblin.ToString(), maxTargets = "3" });
            await Run("sanity/toggle_target", player, new { elementId = goblin.ToString(), maxTargets = "3" });

            Assert.Equal("[]", PropertyValue(gameId, $"sanity_targets_{player.Id}"));
        }

        [Fact]
        public async Task Bless_WithoutTargets_TellsOnlyThePlayer()
        {
            await Run("sanity/bless", player);

            NoErrors();
            Assert.Contains(Chat(player), m => m.Contains("No targets marked"));
            Assert.DoesNotContain(Chat(gm), m => m.Contains("No targets marked"));
        }

        [Fact]
        public async Task Fireball_GmSaysYes_DamagesTheTokensInRange()
        {
            await Run("sanity/fireball", player, new { mapId = mapId.ToString(), position = At(150, 125), radiusSquares = "4", dice = "8d6" });

            NoErrors();
            Assert.Contains(sent, x => x.To == gm && x.Command.Command == "request_input");
            Assert.Equal("0", PropertyValue(goblinCard, "hp"));      // 20 - 24
            Assert.Equal("0", PropertyValue(rat, "bar1_value"));      // 7 - 24
            Assert.Equal("30", PropertyValue(orcCard, "hp"));         // out of range
        }

        [Fact]
        public async Task Fireball_GmSaysHalf_AppliesHalf()
        {
            gmAnswer = "half";
            await Run("sanity/fireball", player, new { mapId = mapId.ToString(), position = At(150, 125), radiusSquares = "4", dice = "8d6" });

            NoErrors();
            Assert.Equal("8", PropertyValue(goblinCard, "hp")); // 20 - 12
        }

        [Fact]
        public async Task Fireball_GmSaysNo_ChangesNothing()
        {
            gmAnswer = "no";
            await Run("sanity/fireball", player, new { mapId = mapId.ToString(), position = At(150, 125), radiusSquares = "4", dice = "8d6" });

            NoErrors();
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
            Assert.Contains(Chat(player), m => m.Contains("didn't apply"));
        }

        // RequestUserInput skips a player who isn't connected; the answer then stays unset.
        [Fact]
        public async Task Fireball_GmOffline_NoDamageWithoutTheirAnswer()
        {
            lobby.ConnectedPlayers.Remove(gm);

            await Run("sanity/fireball", player, new { mapId = mapId.ToString(), position = At(150, 125), radiusSquares = "4", dice = "8d6" });

            NoErrors();
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
            Assert.Contains(Chat(player), m => m.Contains("didn't answer"));
        }

        [Fact]
        public async Task RandomEnemy_CopiesAMarkedCardAndAsksTheGmsBrowserToPlaceIt()
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
                foreach (var id in new[] { goblinCard, orcCard })
                {
                    var card = db.Cards.Single(c => c.Id == id);
                    db.Properties.Add(Property("CardModel", id, "potworny", "true", p => p.Card = card));
                }
                await db.SaveChangesAsync(default);
            }

            await Run("sanity/random_enemy", gm, new { battleMapId = Guid.NewGuid().ToString(), position = At(300, 300) });

            NoErrors();
            var place = Assert.Single(sent, x => x.To == gm && x.Command.Command == "run_client_command");
            Assert.Equal("CreateToken", place.Command.Data!["command"]!.ToString());
            using var check = services.CreateScope();
            var copies = check.ServiceProvider.GetRequiredService<IDbContext>().Cards.Where(c => c.GameId == gameId).Count();
            Assert.Equal(3, copies);
        }

        [Fact]
        public async Task RandomEnemy_NoMarkedCards_TellsTheGm()
        {
            await Run("sanity/random_enemy", gm, new { battleMapId = Guid.NewGuid().ToString(), position = At(300, 300) });

            NoErrors();
            Assert.Contains(Chat(gm), m => m.Contains("potworny"));
        }

        [Fact]
        public async Task DamageAll_EveryTokenOnTheMapTakesTheRoll()
        {
            await Run("sanity/damage_all", gm, new { mapId = mapId.ToString(), dice = "1d6" });

            NoErrors();
            Assert.Equal("17", PropertyValue(goblinCard, "hp"));
            Assert.Equal("4", PropertyValue(rat, "bar1_value"));
            Assert.Equal("27", PropertyValue(orcCard, "hp"));
        }
    }
}

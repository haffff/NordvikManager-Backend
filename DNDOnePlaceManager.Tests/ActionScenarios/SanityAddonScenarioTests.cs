using DndOnePlaceManager.Application;
using DndOnePlaceManager.Application.Commands.Addons.InstallAddon;
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
using DNDOnePlaceManager.WebSockets.Core;
using DNDOnePlaceManager.WebSockets.Handlers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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
        // What the player picks when asked for targets; null cancels.
        private JArray? targetsAnswer;
        // Where the player points when asked for a point; null cancels.
        private JObject? pointAnswer;

        private readonly Guid gameId = Guid.NewGuid();
        private readonly PlayerDTO gm;
        private readonly PlayerDTO player;
        private readonly Guid mapId = Guid.NewGuid();
        private readonly Guid battleMapId = Guid.NewGuid();
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
                ["AddonsConfiguration:MainRepository"] = "http://localhost/unused", // never reached: the addon is a file
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
            db.Add(new DndOnePlaceManager.Domain.Entities.BattleMap.BattleMapModel { Id = battleMapId, Name = "Table", MapId = mapId, Game = game });
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

            db.SaveChanges();

            // The addon goes in as the GM installs it: the zip of ActionScenarios/sanity.
            using var install = services.CreateScope();
            var (response, _) = install.ServiceProvider.GetRequiredService<MediatR.IMediator>().Send(new InstallAddonCommand
            {
                GameID = gameId, Player = gm, AddonFile = AddonZip(), AddonFileName = "sanity.zip",
            }).GetAwaiter().GetResult();
            Assert.Equal(CommandResponse.Ok, response);
        }

        private static byte[] AddonZip()
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "ActionScenarios", "sanity");
            using var stream = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    using (var entry = zip.CreateEntry(Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/')).Open())
                        entry.Write(File.ReadAllBytes(file));
            return stream.ToArray();
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
                    if (command.Command == "request_targets" && command.InputToken is Guid picking)
                        _ = Task.Run(() =>
                        {
                            if (lobby.ActionProcessingService.InputHandler.TryRemove(picking, out var answer))
                                answer.TrySetResult(new WebSocketCommand { Data = (JToken?)targetsAnswer ?? new JObject { ["cancelled"] = true } });
                        });
                    if (command.Command == "request_point" && command.InputToken is Guid aiming)
                        _ = Task.Run(() =>
                        {
                            if (lobby.ActionProcessingService.InputHandler.TryRemove(aiming, out var answer))
                                answer.TrySetResult(new WebSocketCommand { Data = (JToken?)pointAnswer ?? new JObject { ["cancelled"] = true } });
                        });
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
            Assert.Equal(new[] { "sanity_menu", "sanity_bless", "sanity_fireball", "sanity_burning_hands" }, Added(player));
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

        // As the game fires it when a player's client has loaded (GameLobby → Hook.Load).
        [Fact]
        public async Task Setup_RunsFromThePlayerLoadHook()
        {
            await lobby.ActionProcessingService.CallHookAsync(Hook.Load, new PlayerHookArgs { Player = gm });

            NoErrors();
            Assert.Contains("sanity_fireball", Added(gm));
            Assert.Contains("sanity_damage_all", Added(gm));
            Assert.Empty(Added(player));
        }

        [Fact]
        public async Task GmActions_RefusePlayers()
        {
            await Run("sanity/damage_all", player, new { mapId = mapId.ToString(), dice = "1d6" });

            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
        }

        // The player picks up to 3 tokens on the map (RequestTargets), then each gets blessed.
        [Fact]
        public async Task Bless_OnThePickedTargets()
        {
            targetsAnswer = new JArray(goblin.ToString(), rat.ToString(), farOrc.ToString());

            await Run("sanity/bless", player);

            NoErrors();
            var asked = Assert.Single(sent, x => x.To == player && x.Command.Command == "request_targets");
            Assert.Equal(3, (int)asked.Command.Data!["max"]!);
            Assert.Equal("true", PropertyValue(goblin, "sanity_blessed"));
            Assert.Equal("true", PropertyValue(rat, "sanity_blessed"));
            Assert.Equal("true", PropertyValue(farOrc, "sanity_blessed"));
            Assert.Contains(Chat(gm), m => m.Contains("Bless on Goblin, Rat, Far orc"));
        }

        [Fact]
        public async Task Bless_Cancelled_DoesNothing()
        {
            targetsAnswer = null;

            await Run("sanity/bless", player);

            NoErrors();
            Assert.Null(PropertyValue(goblin, "sanity_blessed"));
            Assert.DoesNotContain(Chat(gm), m => m.Contains("Bless on"));
        }

        // As the Sanity menu's Fireball item runs it; the player then points at (150,125).
        private Task Fireball()
        {
            pointAnswer ??= new JObject { ["x"] = 150, ["y"] = 125, ["mapId"] = mapId.ToString() };
            return Run("sanity/fireball", player, new { mapId = mapId.ToString(), radiusSquares = "4", dice = "8d6" });
        }

        [Fact]
        public async Task Fireball_GmSaysYes_DamagesTheTokensInRange()
        {
            await Fireball();

            NoErrors();
            // The player aims with the blast following the cursor, snapped to grid corners.
            var aim = Assert.Single(sent, x => x.To == player && x.Command.Command == "request_point");
            Assert.Equal(200, (double)aim.Command.Data!["area"]!["radius"]!);
            Assert.Equal("corner", (string?)aim.Command.Data!["snap"]);
            Assert.Contains(sent, x => x.To == gm && x.Command.Command == "request_input");
            // The blast is shown on the map while the GM decides, and goes away after.
            var marker = Assert.Single(sent, x => x.To == player && x.Command.Command == "preview_start");
            Assert.Equal(battleMapId.ToString(), marker.Command.BattleMapId);
            Assert.Equal(200, (double)marker.Command.Data![0]!["radius"]!);
            Assert.Contains(sent, x => x.To == player && x.Command.Command == "preview_end");
            Assert.Equal("0", PropertyValue(goblinCard, "hp"));      // 20 - 24
            Assert.Equal("0", PropertyValue(rat, "bar1_value"));      // 7 - 24
            Assert.Equal("30", PropertyValue(orcCard, "hp"));         // out of range
            // Targets may be hidden from players: only the GM learns who was hit and their HP.
            Assert.Contains(Chat(gm), m => m.Contains("Goblin: -24 HP"));
            Assert.DoesNotContain(Chat(player), m => m.Contains("Goblin") || m.Contains("Rat"));
        }

        [Fact]
        public async Task Fireball_GmSaysHalf_AppliesHalf()
        {
            gmAnswer = "half";
            await Fireball();

            NoErrors();
            Assert.Equal("8", PropertyValue(goblinCard, "hp")); // 20 - 12
        }

        [Fact]
        public async Task Fireball_GmSaysNo_ChangesNothing()
        {
            gmAnswer = "no";
            await Fireball();

            NoErrors();
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
            Assert.Contains(Chat(player), m => m.Contains("didn't apply"));
        }

        // Burning Hands: the player picks their character and aims a 15 ft cone from it.
        [Fact]
        public async Task BurningHands_AConeFromTheCaster_HitsWhatItCovers_NotTheCaster()
        {
            targetsAnswer = new JArray(goblin.ToString());
            pointAnswer = new JObject { ["x"] = 400, ["y"] = 125, ["mapId"] = mapId.ToString() }; // east, toward the rat

            await Run("sanity/burning_hands", player, new { mapId = mapId.ToString(), lengthSquares = "3", dice = "3d6" });

            NoErrors();
            var aim = Assert.Single(sent, x => x.To == player && x.Command.Command == "request_point");
            Assert.Equal("cone", (string?)aim.Command.Data!["area"]!["shape"]);
            Assert.Equal(125, (double)aim.Command.Data!["from"]!["x"]!);
            // The cone shown while the GM decides starts at the caster.
            var shown = (JObject)((JArray)Assert.Single(sent, x => x.To == player && x.Command.Command == "preview_start").Command.Data!)[0]!;
            Assert.Equal("polygon", (string?)shown["type"]);
            Assert.Equal("0", PropertyValue(rat, "bar1_value"));  // 7 - 9
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));  // the caster
            Assert.Equal("30", PropertyValue(orcCard, "hp"));     // far away
        }

        [Fact]
        public async Task Fireball_PlayerStopsAiming_NothingHappens()
        {
            pointAnswer = new JObject { ["cancelled"] = true };

            await Fireball();

            NoErrors();
            Assert.DoesNotContain(sent, x => x.Command.Command == "request_input");
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
        }

        // RequestUserInput skips a player who isn't connected; the answer then stays unset.
        [Fact]
        public async Task Fireball_GmOffline_NoDamageWithoutTheirAnswer()
        {
            lobby.ConnectedPlayers.Remove(gm);

            await Fireball();

            NoErrors();
            Assert.Equal("20", PropertyValue(goblinCard, "hp"));
            Assert.Contains(Chat(player), m => m.Contains("didn't answer"));
        }

        [Fact]
        // The server places the token itself (SpawnToken): no browser involved.
        public async Task RandomEnemy_CopiesAMarkedCardAndPlacesItsToken()
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
                foreach (var id in new[] { goblinCard, orcCard })
                {
                    var card = db.Cards.Single(c => c.Id == id);
                    db.Properties.Add(Property("CardModel", id, "potworny", "true", p => p.Card = card));
                    db.Properties.Add(Property("CardModel", id, "token", "sanity_token.json", p => p.Card = card));
                }
                db.Resources.Add(new DndOnePlaceManager.Domain.Entities.Resources.ResourceModel
                {
                    Id = Guid.NewGuid(), GameId = gameId, PlayerId = gm.Id!.Value, Name = "sanity_token.json", Key = "sanity_token.json",
                    MimeType = MimeType.JSON, Data = System.Text.Encoding.UTF8.GetBytes("{\"size\": 1, \"object\": {\"type\": \"image\"}}"),
                });
                await db.SaveChangesAsync(default);
            }

            await Run("sanity/random_enemy", gm, new { battleMapId = battleMapId.ToString(), mapId = mapId.ToString(), position = At(300, 300) });

            NoErrors();
            Assert.DoesNotContain(sent, x => x.Command.Command == "run_client_command");
            Assert.Contains(sent, x => x.To == player && x.Command.Command == "element_add");
            using var check = services.CreateScope();
            var after = check.ServiceProvider.GetRequiredService<IDbContext>();
            Assert.Equal(3, after.Cards.Count(c => c.GameId == gameId));
            var copy = after.Cards.Single(c => c.GameId == gameId && c.Id != goblinCard && c.Id != orcCard);
            var placed = await check.ServiceProvider.GetRequiredService<MediatR.IMediator>().Send(
                new DndOnePlaceManager.Application.Commands.Actions.Find.FindCommand { GameId = gameId, What = "tokens", MapId = mapId });
            var token = Assert.Single(placed, t => (string?)t["cardId"] == copy.Id.ToString());
            Assert.Equal(copy.Name, (string?)token["name"]);
            // The copy isn't one to pick from next time.
            Assert.Equal(2, after.Properties.Count(p => p.Name == "potworny"));
        }

        [Fact]
        public async Task RandomEnemy_NoMarkedCards_TellsTheGm()
        {
            await Run("sanity/random_enemy", gm, new { battleMapId = Guid.NewGuid().ToString(), position = At(300, 300) });

            NoErrors();
            Assert.Contains(Chat(gm), m => m.Contains("potworny"));
        }

        // AddElement: a shape saved on the map, added like a player's (everyone sees it, hooks fire).
        [Fact]
        public async Task AddElement_PutsAShapeOnTheMap()
        {
            var action = new ActionDto
            {
                Id = Guid.NewGuid(), Name = "wall", Prefix = "test", IsEnabled = true,
                Content = new JArray(new JObject
                {
                    ["Type"] = "AddElement",
                    ["Data"] = new JObject { ["Shape"] = "rect", ["At"] = "100,50", ["Width"] = "3", ["Height"] = "1", ["Output"] = "wallId" },
                }).ToString(),
            };

            await lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = gm, Data = new JObject(), Command = new WebSocketCommand { Command = "execute_action" } },
                new Dictionary<string, object> { ["mapId"] = mapId.ToString() });

            NoErrors();
            using var scope = services.CreateScope();
            var found = await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>().Send(
                new DndOnePlaceManager.Application.Commands.Actions.Find.FindCommand { GameId = gameId, What = "elements", MapId = mapId });
            var wall = Assert.Single(found, e => (string?)e["type"] == "rect");
            Assert.Equal(152, (double)wall["w"]!); // 3 squares and its outline, as the map measures it
            Assert.Contains(sent, x => x.To == player && x.Command.Command == "element_add");
        }

        // A cone saved on the map is the triangle Find uses, and Find knows where it is.
        [Fact]
        public async Task AddElement_ACone_IsSavedWhereItIs()
        {
            var action = new ActionDto
            {
                Id = Guid.NewGuid(), Name = "cone", Prefix = "test", IsEnabled = true,
                Content = new JArray(new JObject
                {
                    ["Type"] = "AddElement",
                    ["Data"] = new JObject { ["Shape"] = "cone", ["At"] = "100,100", ["Toward"] = "200,100", ["Radius"] = "3" },
                }).ToString(),
            };

            await lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = gm, Data = new JObject(), Command = new WebSocketCommand { Command = "execute_action" } },
                new Dictionary<string, object> { ["mapId"] = mapId.ToString() });

            NoErrors();
            using var scope = services.CreateScope();
            var found = await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>().Send(
                new DndOnePlaceManager.Application.Commands.Actions.Find.FindCommand { GameId = gameId, What = "elements", MapId = mapId });
            var cone = Assert.Single(found, e => (string?)e["type"] == "polygon");
            // Corners (100,100), (234.2,33.1), (234.2,166.9): its box is centred on (167.1, 100).
            Assert.Equal(167.1, (double)cone["x"]!, 1);
            Assert.Equal(100, (double)cone["y"]!, 1);
        }

        // ── RunScript's game API: the steps, from a script ─────────────────────────

        private async Task RunScript(string script)
        {
            var action = new ActionDto
            {
                Id = Guid.NewGuid(), Name = "script", Prefix = "test", IsEnabled = true,
                Content = new JArray(new JObject { ["Type"] = "RunScript", ["Data"] = new JObject { ["Script"] = script, ["Output"] = "" } },
                    new JObject { ["Type"] = "SetVariable", ["Data"] = new JObject { ["Name"] = "done", ["Value"] = "yes" } }).ToString(),
            };
            await lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = gm, Data = new JObject(), Command = new WebSocketCommand { Command = "execute_action" } },
                new Dictionary<string, object> { ["mapId"] = mapId.ToString() });
        }

        [Fact]
        public async Task Script_FindsReadsAndWrites()
        {
            await RunScript(@"
                var tokens = game.find('tokens');
                var goblin = tokens.filter(function (t) { return t.name === 'Goblin'; })[0];
                var hp = Number(game.getProperty(goblin.cardId, 'hp', '0'));
                game.setProperty(goblin.cardId, 'hp', hp - 5);
                game.chat('Goblin: ' + (hp - 5), { title: 'HP' });
                return { count: tokens.length };");

            NoErrors();
            Assert.Equal("15", PropertyValue(goblinCard, "hp"));
            Assert.Contains(Chat(gm), m => m.Contains("Goblin: 15"));
        }

        // Options are plain JavaScript values: a point object, a number.
        [Fact]
        public async Task Script_FindsNearAPointObject()
        {
            await RunScript(@"
                var near = game.find('tokens', { near: { x: 150, y: 125 }, radius: 4, withCard: true });
                game.setProperty(vars.mapId, 'near', near.map(function (t) { return t.name; }).sort().join(','));
                return {};");

            NoErrors();
            Assert.Equal("Goblin", PropertyValue(mapId, "near"));
        }

        [Fact]
        public async Task Script_RunsAnyStep_AndGetsItsOutput()
        {
            await RunScript(@"
                var r = game.roll('2d6');
                game.step('SetProperty', { ParentId: vars.mapId, PropertyName: 'lastRoll', PropertyValue: String(r.result) });
                return {};");

            NoErrors();
            Assert.Equal("6", PropertyValue(mapId, "lastRoll")); // every die shows 3
        }

        [Fact]
        public async Task Script_CantAskPlayersOrBranch()
        {
            await RunScript("game.step('RequestUserInput', { UserID: vars.gmId, Output: 'x' }); return {};");

            Assert.Contains(lobby.EventLog.GetEntries(), e => e.Level == "Error" && e.Message.Contains("RequestUserInput"));
        }

        // Protected properties (API keys etc.) never reach a script.
        [Fact]
        public async Task Script_NeverReadsProtectedValues()
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
                var card = db.Cards.Single(c => c.Id == goblinCard);
                var secret = Property("CardModel", goblinCard, "apiKey", "s3cret", p => p.Card = card);
                secret.IsProtected = true;
                db.Properties.Add(secret);
                await db.SaveChangesAsync(default);
            }

            await RunScript($"game.setProperty(vars.mapId, 'leak', game.getProperty('{goblinCard}', 'apiKey', 'none')); return {{}};");

            NoErrors();
            Assert.NotEqual("s3cret", PropertyValue(mapId, "leak"));
        }

        // SetProperty Protected: an addon's settings view saves an API key (e.g. Trilium's
        // ETAPI token) for Send Request's bearer token. No client and no later step sees it.
        private Task RunSteps(params JObject[] steps) => RunStepsAs(gm, steps);

        private async Task RunStepsAs(PlayerDTO by, params JObject[] steps)
        {
            var action = new ActionDto { Id = Guid.NewGuid(), Name = "steps", Prefix = "test", IsEnabled = true, Content = new JArray(steps).ToString() };
            await lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = by, Data = new JObject(), Command = new WebSocketCommand { Command = "execute_action" } },
                new Dictionary<string, object> { ["mapId"] = mapId.ToString() });
        }

        private static JObject SetGameProperty(string name, string value, bool? isProtected = null)
        {
            var data = new JObject { ["Of"] = "game", ["PropertyName"] = name, ["PropertyValue"] = value };
            if (isProtected != null) data["Protected"] = isProtected.Value;
            return new JObject { ["Type"] = "SetProperty", ["Data"] = data };
        }

        private PropertyModel? GameProperty(string name)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
            return db.Properties.AsNoTracking().FirstOrDefault(p => p.ParentID == gameId && p.Name == name);
        }

        private void NothingSentContains(string text) =>
            Assert.DoesNotContain(sent, s => JsonConvert.SerializeObject(s.Command).Contains(text));

        [Fact]
        public async Task SetProperty_Protected_IsStoredProtected_AndNoClientGetsTheValue()
        {
            await RunSteps(SetGameProperty("trilium_token", "etapi-secret", isProtected: true));

            NoErrors();
            var stored = GameProperty("trilium_token")!;
            Assert.True(stored.IsProtected);
            Assert.Equal("etapi-secret", stored.Value);
            NothingSentContains("etapi-secret");
        }

        [Fact]
        public async Task SetProperty_Protected_ReplacesTheSecret_WithoutSendingIt()
        {
            await RunSteps(SetGameProperty("trilium_token", "first-secret", isProtected: true));

            await RunSteps(SetGameProperty("trilium_token", "second-secret", isProtected: true));

            NoErrors();
            Assert.Equal("second-secret", GameProperty("trilium_token")!.Value);
            NothingSentContains("first-secret");
            NothingSentContains("second-secret");
        }

        // A plain SetProperty on it later never makes it readable.
        [Fact]
        public async Task SetProperty_WithoutProtected_OnAProtectedProperty_KeepsItProtected()
        {
            await RunSteps(SetGameProperty("trilium_token", "etapi-secret", isProtected: true));

            await RunSteps(SetGameProperty("trilium_token", "new-secret"));

            NoErrors();
            var stored = GameProperty("trilium_token")!;
            Assert.True(stored.IsProtected);
            NothingSentContains("new-secret");
        }

        [Fact]
        public async Task SetProperty_Protected_LaterStepsReadNothing()
        {
            await RunSteps(
                SetGameProperty("trilium_token", "etapi-secret", isProtected: true),
                new JObject { ["Type"] = "GetPropertyValue", ["Data"] = new JObject { ["Of"] = "game", ["PropertyName"] = "trilium_token", ["Output"] = "t" } },
                new JObject { ["Type"] = "SetProperty", ["Data"] = new JObject { ["Of"] = "map", ["PropertyName"] = "leak", ["PropertyValue"] = "%t%" } });

            Assert.NotEqual("etapi-secret", PropertyValue(mapId, "leak"));
            Assert.DoesNotContain(lobby.EventLog.GetEntries(), e => (e.Message ?? "").Contains("etapi-secret"));
        }

        // SetPermission: share a card for viewing (e.g. a Trilium lore note), only Read or
        // Control, only by someone who has it; the players who gain or lose it are told.
        private static JObject SetPermission(Guid entity, string player, string permission = "Read", bool revoke = false) =>
            new() { ["Type"] = "SetPermission", ["Data"] = new JObject { ["EntityId"] = entity.ToString(), ["Player"] = player, ["Permission"] = permission, ["Revoke"] = revoke } };

        private bool PlayerCanRead(Guid entity)
        {
            using var scope = services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<DndOnePlaceManager.Application.Services.IPermissionService>()
                .CheckIfHasPermissions(player, entity, Permission.Read);
        }

        private List<WebSocketCommand> SentTo(PlayerDTO to, string command) =>
            sent.Where(x => x.To == to && x.Command.Command == command).Select(x => x.Command).ToList();

        [Fact]
        public async Task SetPermission_SharesACard_AndTellsOnlyThatPlayer()
        {
            await RunSteps(SetPermission(goblinCard, player.Id.ToString()!));

            NoErrors();
            Assert.True(PlayerCanRead(goblinCard));
            var added = Assert.Single(SentTo(player, WebSocketCommandNames.CardAdd));
            Assert.Equal(goblinCard.ToString(), added.Data!["id"]!.ToString());
            Assert.Empty(SentTo(gm, WebSocketCommandNames.CardAdd));
            Assert.Contains(lobby.EventLog.GetEntries(), e => e.Message.Contains("SetPermission") && e.Message.Contains("Goblin"));
        }

        [Fact]
        public async Task SetPermission_Unshares_AndTheCardLeavesThatPlayersList()
        {
            await RunSteps(SetPermission(goblinCard, player.Name));

            await RunSteps(SetPermission(goblinCard, player.Name, revoke: true));

            NoErrors();
            Assert.False(PlayerCanRead(goblinCard));
            Assert.Single(SentTo(player, WebSocketCommandNames.CardDelete));
        }

        [Fact]
        public async Task SetPermission_ByAPlayer_IsRefused()
        {
            await RunStepsAs(player, SetPermission(goblinCard, player.Name));

            Assert.Contains(lobby.EventLog.GetEntries(), e => e.Level == "Error");
            Assert.False(PlayerCanRead(goblinCard));
        }

        [Fact]
        public async Task SetPermission_Edit_IsRefused()
        {
            await RunSteps(SetPermission(goblinCard, player.Name, "Edit"));

            Assert.Contains(lobby.EventLog.GetEntries(), e => e.Level == "Error" && e.Message.Contains("Read"));
            Assert.False(PlayerCanRead(goblinCard));
        }

        // Include: shared script files (an addon's resources) go before the script.
        [Fact]
        public async Task Script_IncludesSharedFiles()
        {
            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
                db.Resources.Add(new DndOnePlaceManager.Domain.Entities.Resources.ResourceModel
                {
                    Id = Guid.NewGuid(), GameId = gameId, PlayerId = gm.Id!.Value, Name = "sanity_lib.js", Key = "sanity_lib.js",
                    MimeType = MimeType.JavaScript, Data = System.Text.Encoding.UTF8.GetBytes("function twice(x) { return x * 2; }"),
                });
                await db.SaveChangesAsync(default);
            }
            var action = new ActionDto
            {
                Id = Guid.NewGuid(), Name = "inc", Prefix = "test", IsEnabled = true,
                Content = new JArray(
                    new JObject { ["Type"] = "RunScript", ["Data"] = new JObject { ["Script"] = "return { n: twice(21) };", ["Include"] = "sanity_lib.js", ["Output"] = "" } },
                    new JObject { ["Type"] = "SetProperty", ["Data"] = new JObject { ["Of"] = "map", ["PropertyName"] = "n", ["PropertyValue"] = "%n%" } }).ToString(),
            };

            await lobby.ActionProcessingService.ExecActionAsync(action,
                new CommandHookArgs { Player = gm, Data = new JObject(), Command = new WebSocketCommand { Command = "execute_action" } },
                new Dictionary<string, object> { ["mapId"] = mapId.ToString() });

            NoErrors();
            Assert.Equal("42", PropertyValue(mapId, "n"));
        }

        [Fact]
        public async Task Script_HasACallLimit()
        {
            await RunScript("for (var i = 0; i < 300; i++) game.log('x'); return {};");

            Assert.Contains(lobby.EventLog.GetEntries(), e => e.Level == "Error" && e.Message.Contains("200"));
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

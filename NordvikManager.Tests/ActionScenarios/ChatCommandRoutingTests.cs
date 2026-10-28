using NordvikManager.Application;
using NordvikManager.Application.Commands.Actions;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Security;
using NordvikManager.Domain.Enums;
using NordvikManager.Infrastructure;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Data.Contexts;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Enums;
using NordvikManager.Services;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using NordvikManager.WebSockets;
using NordvikManager.WebSockets.Core;
using NordvikManager.WebSockets.Handlers;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.ActionScenarios
{
    // Chat Command actions get only the commands their hook filter lists; any other "/name"
    // still goes to the chat, which answers "Wrong command". Real lobby, steps and handlers
    // over an in-memory SQLite game; only the players' browsers are stood in for.
    [Collection("ActionScenarios")]
    public class ChatCommandRoutingTests : IDisposable
    {
        private readonly SqliteConnection keepAlive;
        private readonly ServiceProvider services;
        private readonly GameLobby lobby;
        private readonly List<(PlayerDTO To, WebSocketCommand Command)> sent = new();
        private readonly Guid gameId = Guid.NewGuid();
        private readonly PlayerDTO gm, player;
        private readonly Xunit.Abstractions.ITestOutputHelper output;

        public ChatCommandRoutingTests(Xunit.Abstractions.ITestOutputHelper output)
        {
            this.output = output;
            var connectionString = $"Data Source=chatroute{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            keepAlive = new SqliteConnection(connectionString);
            keepAlive.Open();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseSqlite"] = "true",
                ["ConnectionStrings:DBData"] = connectionString,
                ["AddonsConfiguration:MainRepository"] = "http://localhost/unused",
            }).Build();

            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<IConfiguration>(config);
            InfrastructureLayerModule.Register(collection, config);
            ApplicationLayerModule.Register(collection, config);
            collection.AddScoped<NordvikManager.Application.Interfaces.IGameEventLogger, LobbyGameEventLogger>();
            collection.AddSingleton(new Mock<IPlaybackService>().Object);
            collection.AddScoped(typeof(IActionProcessingService), typeof(IActionProcessingService).Assembly
                .GetType("NordvikManager.Services.Implementations.ActionProcessingService")!);
            foreach (var type in typeof(GameLobby).Assembly.GetTypes().Where(t => !t.IsAbstract && t.IsClass))
            {
                if (typeof(IWebSocketHandler).IsAssignableFrom(type)) collection.AddScoped(typeof(IWebSocketHandler), type);
                if (typeof(IActionStepDefinition).IsAssignableFrom(type)) collection.AddSingleton(typeof(IActionStepDefinition), type);
            }
            services = collection.BuildServiceProvider();
            ApplicationLayerModule.AfterBuild(services);

            gm = new PlayerDTO { Id = Guid.NewGuid(), Name = "GM", IsOwner = true };
            player = new PlayerDTO { Id = Guid.NewGuid(), Name = "Ana" };
            var systemId = Guid.NewGuid();

            using (var scope = services.CreateScope())
            {
                var db = (DndOneContext)scope.ServiceProvider.GetRequiredService<IDbContext>();
                db.Database.EnsureCreated();
                db.Add(new GameModel
                {
                    Id = gameId, Name = "Table", MasterId = gm.Id!.Value, SystemPlayerId = systemId,
                    Players = new List<PlayerModel>
                    {
                        new() { Id = gm.Id.Value, Name = gm.Name },
                        new() { Id = player.Id!.Value, Name = player.Name },
                        new() { Id = systemId, Name = "System", System = true },
                    },
                });
                db.Set<PermissionModel>().AddRange(
                    new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = gm.Id.Value, Permission = Permission.All },
                    new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = systemId, Permission = Permission.All },
                    new PermissionModel { Id = Guid.NewGuid(), ModelID = gameId, PlayerID = player.Id.Value, Permission = Permission.Read });
                db.SaveChanges();
            }

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
            foreach (var e in lobby.EventLog.GetEntries())
                output.WriteLine($"[{e.Level}] {e.Category}: {e.Message}");
            lock (sent)
                foreach (var (to, command) in sent)
                    output.WriteLine($"-> {to.Name}: {command.Command} {command.Data?.ToString(Newtonsoft.Json.Formatting.None)}");
            services.Dispose();
            keepAlive.Dispose();
        }

        private IPlayerConnection Connection(PlayerDTO who)
        {
            var connection = new Mock<IPlayerConnection>();
            connection.Setup(c => c.SendMessageToPlayer(It.IsAny<object>()))
                .Callback<object>(m => { if (m is WebSocketCommand c) lock (sent) sent.Add((who, c)); })
                .ReturnsAsync(true);
            return connection.Object;
        }

        // A Chat Command action that whispers "<tag> got /<command> <args>" back.
        private async Task AddChatAction(string tag, string? hookFilter, bool enabled = true)
        {
            using var scope = services.CreateScope();
            var content = new JArray(new JObject
            {
                ["Type"] = "SendChat",
                ["Data"] = new JObject
                {
                    ["Template"] = "Text",
                    ["Message"] = $"{tag} got /%ChatCommand% %ChatArgs%",
                    ["Player"] = "%playerId%",
                },
            });
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AddActionCommand
            {
                GameId = gameId,
                Player = gm,
                Action = new ActionDto
                {
                    Name = tag, Description = "", Prefix = "test", Hook = Hook.ChatCommand,
                    HookFilter = hookFilter, IsEnabled = enabled, Content = content.ToString(),
                },
            });
        }

        private Task Say(PlayerDTO who, string text) =>
            lobby.HandleCommand(who, new WebSocketCommand { Command = WebSocketCommandNames.CmdChatPush, Data = text });

        private List<string> ChatTo(PlayerDTO who)
        {
            lock (sent)
                return sent.Where(x => x.To == who && x.Command.Command.StartsWith("chat"))
                    .Select(x => x.Command.Data?.ToString() ?? "").ToList();
        }

        // The hook runs in the background: wait a little for its whisper.
        private async Task<bool> Eventually(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 5000)
            {
                if (condition()) return true;
                await Task.Delay(25);
            }
            return condition();
        }

        [Fact]
        public async Task ListedCommand_RunsOnlyTheActionsThatListIt()
        {
            await AddChatAction("Polls", "poll, vote");
            await AddChatAction("Spells", "cast");

            await Say(player, "/VOTE 2");

            Assert.True(await Eventually(() => ChatTo(player).Any(m => m.Contains("Polls got /VOTE 2"))), "the poll action didn't run");
            await Task.Delay(200);
            Assert.DoesNotContain(ChatTo(player), m => m.Contains("Spells got"));
            Assert.DoesNotContain(ChatTo(player), m => m.Contains("Wrong command"));
            Assert.DoesNotContain(ChatTo(gm), m => m.Contains("/VOTE 2"));
        }

        [Fact]
        public async Task UnlistedCommand_StillGetsWrongCommand_WithChatActionsInstalled()
        {
            await AddChatAction("Polls", "poll, vote");

            await Say(player, "/nope 1");

            Assert.True(await Eventually(() => ChatTo(player).Any(m => m.Contains("Wrong command"))), "no 'Wrong command'");
            await Task.Delay(200);
            Assert.DoesNotContain(ChatTo(player), m => m.Contains("Polls got"));
        }

        [Fact]
        public async Task DisabledAction_DoesNotClaimItsCommand()
        {
            await AddChatAction("Polls", "poll", enabled: false);

            await Say(player, "/poll");

            Assert.True(await Eventually(() => ChatTo(player).Any(m => m.Contains("Wrong command"))), "no 'Wrong command'");
        }
    }
}

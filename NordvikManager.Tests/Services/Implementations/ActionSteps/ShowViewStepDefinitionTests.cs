using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Application.Exceptions;
using NordvikManager.Domain.Entities.BattleMap;
using NordvikManager.Infrastructure.Interfaces;
using NordvikManager.Data.Contexts;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.ActionBody;
using NordvikManager.Services.Implementations.ActionSteps;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebSockets;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.Services.Implementations.ActionSteps
{
    // Show View opens a view (a custom UI card) by its key. Only views count: a card that
    // took the key first (keys are unique per game) must not be shown in the view's place.
    public class ShowViewStepDefinitionTests : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DndOneContext _db;
        private readonly GameLobby _lobby;
        private readonly ShowViewStepDefinition _step;

        public ShowViewStepDefinitionTests()
        {
            _connection.Open();
            _db = new DndOneContext(new DbContextOptionsBuilder<DndOneContext>().UseSqlite(_connection).Options);
            _db.Database.EnsureCreated();

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IDbContext))).Returns(_db);
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var factory = new Mock<IServiceScopeFactory>();
            factory.Setup(f => f.CreateScope()).Returns(scope.Object);

            var game = new GameModel { Id = Guid.NewGuid(), Name = "Game", SystemPlayerId = Guid.NewGuid() };
            _db.Games.Add(game);
            _db.SaveChanges();

            _lobby = new GameLobby(factory.Object) { GameId = game.Id, SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            _step = new ShowViewStepDefinition(factory.Object);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }

        private void AddCard(string key, bool isView)
        {
            _db.Cards.Add(new CardModel { Id = Guid.NewGuid(), Name = key, Key = key, GameId = _lobby.GameId, IsCustomUi = isView, Properties = new() });
            _db.SaveChanges();
        }

        private Task Run(string viewKey) =>
            _step.Execute(new Mock<IMediator>().Object, new Dictionary<string, object>(), _lobby,
                new ActionStep { Type = "ShowView", Data = JObject.FromObject(new { ViewKey = viewKey }) });

        [Fact]
        public async Task OnlyAPlainCardWithTheKey_IsNoView()
        {
            AddCard("trilium_viewer", isView: false);

            var e = await Assert.ThrowsAsync<ActionProcessException>(() => Run("trilium_viewer"));

            Assert.Contains("no view", e.Message);
        }

        [Fact]
        public async Task TheView_IsFound()
        {
            AddCard("trilium_viewer", isView: true);

            await Run("trilium_viewer");
        }
    }
}

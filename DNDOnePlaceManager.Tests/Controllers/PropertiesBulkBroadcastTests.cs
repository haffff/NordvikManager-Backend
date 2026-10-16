using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.Commands.Properties.AddProperties;
using DndOnePlaceManager.Application.Commands.Properties.GetPropertiesByQuery;
using DndOnePlaceManager.Application.Commands.Properties.GetTokenViewers;
using DndOnePlaceManager.Application.Commands.Properties.UpdateProperties;
using DndOnePlaceManager.Application.Commands.Security.GetPermissions;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DndOnePlaceManager.Domain.Enums;
using DNDOnePlaceManager.Controllers;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DNDOnePlaceManager.Tests.Controllers
{
    // properties/UpdateBulk and AddBulk (the settings panels) save over REST. Every
    // client — open cards (Properties.Subscribe), tokens, other panels — learns about the
    // change the same way as for a single change: a property_update / property_add per
    // row, sent only to players who can read it.
    public class PropertiesBulkBroadcastTests
    {
        private sealed class WireConnection : IPlayerConnection
        {
            public List<JObject> Sent { get; } = new();
            public Task<bool> SendMessageToPlayer(object message)
            {
                Sent.Add(JObject.Parse(JsonConvert.SerializeObject(message)));
                return Task.FromResult(true);
            }
        }

        private readonly Mock<IMediator> _mediator = new();
        private readonly Guid _cardId = Guid.NewGuid();
        private readonly PlayerDTO _gm = new() { Id = Guid.NewGuid(), Name = "GM" };
        private readonly PlayerDTO _reader = new() { Id = Guid.NewGuid(), Name = "Can read the card" };
        private readonly PlayerDTO _stranger = new() { Id = Guid.NewGuid(), Name = "Cannot" };
        private readonly Dictionary<PlayerDTO, WireConnection> _connections = new();
        private readonly GameLobby _lobby;
        private readonly PropertiesController _controller;

        public PropertiesBulkBroadcastTests()
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new GetPlayerCommandResponse { Player = _gm });
            _mediator.Setup(m => m.Send(It.IsAny<UpdatePropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
            _mediator.Setup(m => m.Send(It.IsAny<AddPropertiesCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(CommandResponse.Ok);
            _mediator.Setup(m => m.Send(It.IsAny<GetPermissionsCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Dictionary<Guid, Permission> { [_gm.Id!.Value] = Permission.All, [_reader.Id!.Value] = Permission.Read });
            _mediator.Setup(m => m.Send(It.IsAny<GetTokenViewersCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new List<Guid>());

            var scopes = new ServiceCollection()
                .AddSingleton(_mediator.Object)
                .AddSingleton(new Mock<DNDOnePlaceManager.Services.Interfaces.IActionProcessingService>().Object)
                .BuildServiceProvider()
                .GetRequiredService<IServiceScopeFactory>();
            _lobby = new GameLobby(scopes) { GameId = Guid.NewGuid(), SystemPlayer = new PlayerDTO { Id = Guid.NewGuid(), Name = "System" } };
            foreach (var player in new[] { _gm, _reader, _stranger })
            {
                var connection = new WireConnection();
                _connections[player] = connection;
                _lobby.ConnectedPlayers[player] = new List<IPlayerConnection> { connection };
            }

            var lobbies = new Mock<ILobbyService>();
            lobbies.Setup(l => l.GetLobby(_lobby.GameId)).Returns(_lobby);
            _controller = new PropertiesController(_mediator.Object, lobbies.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.Items["User"] = new User { Id = "gm", UserName = "gm" };
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        }

        // What the server has after saving — re-read, never the request echoed back.
        private void Saved(params PropertyDTO[] rows) =>
            _mediator.Setup(m => m.Send(It.IsAny<GetPropertiesByQueryCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(rows.ToList());

        private PropertyDTO Row(string name, string value) =>
            new() { Id = Guid.NewGuid(), Name = name, Value = value, ParentID = _cardId, EntityName = "CardModel" };

        private List<JObject> Received(PlayerDTO player, string command) =>
            _connections[player].Sent.Where(m => m["command"]?.ToString() == command).ToList();

        [Fact]
        public async Task UpdateBulk_SendsAPropertyUpdatePerRow_ToPlayersWhoCanRead()
        {
            var hp = Row("hp", "12");
            var ac = Row("ac", "15");
            Saved(hp, ac);

            var result = await _controller.UpdateBulkProperties(_lobby.GameId, new[] { new PropertyDTO { Id = hp.Id, Value = "stale" }, new PropertyDTO { Id = ac.Id } });

            Assert.IsType<OkObjectResult>(result);
            foreach (var player in new[] { _gm, _reader })
            {
                var updates = Received(player, "property_update");
                Assert.Equal(2, updates.Count);
                var first = (JObject)updates[0]["data"]!;
                Assert.Equal(_cardId.ToString(), first["parentId"]?.ToString());
                Assert.Equal("hp", first["name"]?.ToString());
                Assert.Equal("12", first["value"]?.ToString());
            }
            Assert.Empty(Received(_stranger, "property_update"));

            // Re-read by the saved ids.
            _mediator.Verify(m => m.Send(It.Is<GetPropertiesByQueryCommand>(c =>
                c.GameId == _lobby.GameId && c.Ids!.Contains(hp.Id!.Value) && c.Ids!.Contains(ac.Id!.Value)), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddBulk_SendsAPropertyAddPerRow()
        {
            Saved(Row("speed", "30"));

            await _controller.AddBulkProperties(_lobby.GameId, new[] { new PropertyDTO { Name = "speed", Value = "30", ParentID = _cardId } });

            var added = Assert.Single(Received(_reader, "property_add"));
            Assert.Equal("speed", added["data"]?["name"]?.ToString());
            _mediator.Verify(m => m.Send(It.Is<GetPropertiesByQueryCommand>(c =>
                c.ParentIDs!.Single() == _cardId && c.PropertyNames!.Single() == "speed"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Bulk_WithNoRunningGame_StillSaves()
        {
            Saved(Row("hp", "1"));

            var result = await _controller.UpdateBulkProperties(Guid.NewGuid(), new[] { new PropertyDTO { Id = Guid.NewGuid() } });

            Assert.IsType<OkObjectResult>(result);
        }
    }
}

using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using SIPSorcery.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // The player client never logs in to this server, so the WebRTC connection is where a
    // banned user has to be stopped: before it, nothing checked, and a banned user was even
    // created as a player again. A SIPSorcery peer stands in for the browser.
    public class WebRTCSessionServiceBanTests : IDisposable
    {
        private readonly Mock<ISignalingService> _signaling = new();
        private readonly Mock<IMediator> _mediator = new();
        private Func<WebRTCSignalArgs, Task>? _onOffer;
        private Func<IceCandidateArgs, Task>? _onIce;
        private readonly RTCPeerConnection _browser = new(new RTCConfiguration());
        private readonly Guid _gameId = Guid.NewGuid();

        public WebRTCSessionServiceBanTests()
        {
            _signaling.SetupAdd(s => s.OfferReceived += It.IsAny<Func<WebRTCSignalArgs, Task>>())
                .Callback<Func<WebRTCSignalArgs, Task>>(h => _onOffer = h);
            _signaling.SetupAdd(s => s.IceCandidateReceived += It.IsAny<Func<IceCandidateArgs, Task>>())
                .Callback<Func<IceCandidateArgs, Task>>(h => _onIce = h);
            _signaling.Setup(s => s.SendAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((_, _, sdp) =>
                {
                    _browser.setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
                    return Task.CompletedTask;
                });
            _signaling.Setup(s => s.SendIceCandidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .Returns<string, string, string, string?, int?>((_, _, candidate, sdpMid, index) =>
                {
                    if (_browser.remoteDescription != null)
                        _browser.addIceCandidate(new RTCIceCandidateInit { candidate = candidate, sdpMid = sdpMid, sdpMLineIndex = (ushort)(index ?? 0) });
                    return Task.CompletedTask;
                });

            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(IMediator))).Returns(_mediator.Object);
            provider.Setup(p => p.GetService(typeof(IActionProcessingService))).Returns(new Mock<IActionProcessingService>().Object);
            provider.Setup(p => p.GetService(typeof(IEnumerable<NordvikManager.WebSockets.Handlers.IWebSocketHandler>)))
                    .Returns(new List<NordvikManager.WebSockets.Handlers.IWebSocketHandler>());
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            var scopes = new Mock<IServiceScopeFactory>();
            scopes.Setup(f => f.CreateScope()).Returns(scope.Object);

            var registry = new Mock<ILobbyRegistry>();
            var games = new ConcurrentDictionary<Guid, GameLobby>();
            games[_gameId] = new GameLobby(scopes.Object)
            {
                GameId = _gameId,
                ConnectedPlayers = new Dictionary<PlayerDTO, List<IPlayerConnection>>(),
            };
            registry.Setup(r => r.Games).Returns(games);

            _ = new WebRTCSessionService(
                _signaling.Object,
                registry.Object,
                new Mock<IWebRTCApiDispatcher>().Object,
                new Mock<ICentralServerService>().Object,
                scopes.Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                NullLogger<WebRTCSessionService>.Instance);
        }

        public void Dispose() => _browser.close();

        // Connects the browser as user-1 and returns the first message the backend sends it.
        private async Task<string?> FirstMessageAsync()
        {
            var first = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var channel = await _browser.createDataChannel("game");
            channel.onmessage += (_, _, data) => first.TrySetResult(Encoding.UTF8.GetString(data));
            _browser.onicecandidate += c =>
            {
                if (c != null)
                    _ = _onIce!(new IceCandidateArgs(_gameId.ToString(), "socket-1", c.candidate, c.sdpMid, (int?)c.sdpMLineIndex, "user-1"));
            };
            var offer = _browser.createOffer();
            await _browser.setLocalDescription(offer);

            await _onOffer!(new WebRTCSignalArgs(_gameId.ToString(), "socket-1", offer.sdp, "user-1", "Alice"));

            var done = await Task.WhenAny(first.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            return done == first.Task ? first.Task.Result : null;
        }

        [Fact]
        public async Task ABannedUser_IsToldSo_AndNotCreatedAsAPlayer()
        {
            _mediator.Setup(m => m.Send(It.Is<CheckUserBannedCommand>(c => c.CentralUserId == "user-1"), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(true);

            var message = await FirstMessageAsync();

            Assert.NotNull(message);
            Assert.Contains("err: banned", message);
            _mediator.Verify(m => m.Send(It.IsAny<GetPlayerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
            _mediator.Verify(m => m.Send(It.IsAny<AddPlayerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}

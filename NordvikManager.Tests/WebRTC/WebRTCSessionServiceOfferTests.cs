using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using SIPSorcery.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // A player's offer gets an answer even when this backend never saw the player's
    // peer-joined: the player joined before the GM backend (re)connected to signaling, or
    // it re-offers on the same signaling socket after its connection dropped. Central relays
    // who sent the offer, which is enough to set up the peer connection then.
    // A SIPSorcery peer stands in for the browser, wired to the service through fake signaling.
    public class WebRTCSessionServiceOfferTests : IDisposable
    {
        private readonly Mock<ISignalingService> _signaling = new();
        private Func<WebRTCSignalArgs, Task>? _onOffer;
        private Func<PeerJoinedArgs, Task>? _onPeerJoined;
        private Func<IceCandidateArgs, Task>? _onIce;
        private readonly List<string> _answers = new();
        private readonly ConcurrentDictionary<string, RTCPeerConnection> _browsers = new();
        private readonly List<RTCPeerConnection> _opened = new();
        private readonly string _game = Guid.NewGuid().ToString();

        public WebRTCSessionServiceOfferTests()
        {
            _signaling.SetupAdd(s => s.OfferReceived += It.IsAny<Func<WebRTCSignalArgs, Task>>())
                .Callback<Func<WebRTCSignalArgs, Task>>(h => _onOffer = h);
            _signaling.SetupAdd(s => s.PeerJoined += It.IsAny<Func<PeerJoinedArgs, Task>>())
                .Callback<Func<PeerJoinedArgs, Task>>(h => _onPeerJoined = h);
            _signaling.SetupAdd(s => s.IceCandidateReceived += It.IsAny<Func<IceCandidateArgs, Task>>())
                .Callback<Func<IceCandidateArgs, Task>>(h => _onIce = h);
            _signaling.Setup(s => s.SendAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((_, socketId, sdp) =>
                {
                    _answers.Add(socketId);
                    _browsers[socketId].setRemoteDescription(new RTCSessionDescriptionInit { type = RTCSdpType.answer, sdp = sdp });
                    return Task.CompletedTask;
                });
            _signaling.Setup(s => s.SendIceCandidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .Returns<string, string, string, string?, int?>((_, socketId, candidate, sdpMid, index) =>
                {
                    if (_browsers.TryGetValue(socketId, out var browser) && browser.remoteDescription != null)
                        browser.addIceCandidate(new RTCIceCandidateInit { candidate = candidate, sdpMid = sdpMid, sdpMLineIndex = (ushort)(index ?? 0) });
                    return Task.CompletedTask;
                });

            var registry = new Mock<ILobbyRegistry>();
            registry.Setup(r => r.Games).Returns(new ConcurrentDictionary<Guid, GameLobby>());

            _ = new WebRTCSessionService(
                _signaling.Object,
                registry.Object,
                new Mock<IWebRTCApiDispatcher>().Object,
                new Mock<ICentralServerService>().Object,
                new Mock<IServiceScopeFactory>().Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                NullLogger<WebRTCSessionService>.Instance);
        }

        public void Dispose()
        {
            foreach (var browser in _opened) browser.close();
        }

        // A browser peer on `socketId`: its offer, and a task that completes once it connects.
        private async Task<(string Sdp, Task Connected)> BrowserAsync(string socketId)
        {
            var browser = new RTCPeerConnection(new RTCConfiguration());
            _opened.Add(browser);
            _browsers[socketId] = browser;
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            browser.onconnectionstatechange += state =>
            {
                if (state == RTCPeerConnectionState.connected) connected.TrySetResult();
            };
            browser.onicecandidate += c =>
            {
                if (c != null)
                    _ = _onIce!(new IceCandidateArgs(_game, socketId, c.candidate, c.sdpMid, (int?)c.sdpMLineIndex, "user-1"));
            };
            await browser.createDataChannel("game");
            var offer = browser.createOffer();
            await browser.setLocalDescription(offer);
            return (offer.sdp, connected.Task);
        }

        private static async Task ConnectsAsync(Task connected)
        {
            var done = await Task.WhenAny(connected, Task.Delay(TimeSpan.FromSeconds(15)));
            Assert.True(done == connected, "the browser peer never connected");
        }

        [Fact]
        public async Task AnOfferFromAPeerNeverSeenJoining_IsAnsweredAndConnects()
        {
            var (sdp, connected) = await BrowserAsync("socket-1");

            await _onOffer!(new WebRTCSignalArgs(_game, "socket-1", sdp, "user-1", "Alice"));

            Assert.Equal(new[] { "socket-1" }, _answers);
            await ConnectsAsync(connected);
        }

        [Fact]
        public async Task PeerJoinedAfterTheOffer_KeepsThePeerConnectionThatAnswered()
        {
            var (sdp, connected) = await BrowserAsync("socket-1");

            await _onOffer!(new WebRTCSignalArgs(_game, "socket-1", sdp, "user-1", "Alice"));
            await _onPeerJoined!(new PeerJoinedArgs(_game, "user-1", "Alice", "socket-1"));

            // Had peer-joined replaced it, the browser's candidates would go to a connection
            // that never answered, and nothing would connect.
            await ConnectsAsync(connected);
        }

        [Fact]
        public async Task ASecondOfferOnTheSameSocket_GetsAFreshPeerConnection()
        {
            var (firstSdp, firstConnected) = await BrowserAsync("socket-1");
            await _onOffer!(new WebRTCSignalArgs(_game, "socket-1", firstSdp, "user-1", "Alice"));
            await ConnectsAsync(firstConnected);

            // The player's connection dropped; it offers again from a new peer connection.
            var (secondSdp, secondConnected) = await BrowserAsync("socket-1");
            await _onOffer!(new WebRTCSignalArgs(_game, "socket-1", secondSdp, "user-1", "Alice"));

            Assert.Equal(new[] { "socket-1", "socket-1" }, _answers);
            await ConnectsAsync(secondConnected);
        }
    }
}

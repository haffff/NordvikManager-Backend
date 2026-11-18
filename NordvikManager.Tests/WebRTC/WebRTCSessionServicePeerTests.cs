using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // WebRTCSessionService against IPeerConnection, with no real WebRTC stack: what it asks of a
    // peer connection, whichever one the host provides (SIPSorcery natively, the browser's own in wasm).
    public class WebRTCSessionServicePeerTests
    {
        private readonly Mock<ISignalingService> _signaling = new();
        private readonly Mock<ICentralServerService> _central = new();
        private readonly FakePeerFactory _factory = new();
        private Func<WebRTCSignalArgs, Task>? _onOffer;
        private Func<IceCandidateArgs, Task>? _onIce;
        private readonly List<(string Socket, string Sdp)> _answers = new();
        private readonly List<string> _sent = new();
        private readonly string _game = Guid.NewGuid().ToString();
        private readonly WebRTCSessionService _service;

        public WebRTCSessionServicePeerTests()
        {
            _signaling.SetupAdd(s => s.OfferReceived += It.IsAny<Func<WebRTCSignalArgs, Task>>())
                .Callback<Func<WebRTCSignalArgs, Task>>(h => _onOffer = h);
            _signaling.SetupAdd(s => s.IceCandidateReceived += It.IsAny<Func<IceCandidateArgs, Task>>())
                .Callback<Func<IceCandidateArgs, Task>>(h => _onIce = h);
            _signaling.Setup(s => s.SendAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string, string>((_, socket, sdp) =>
                {
                    lock (_sent) { _answers.Add((socket, sdp)); _sent.Add("answer"); }
                    return Task.CompletedTask;
                });
            _signaling.Setup(s => s.SendIceCandidateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .Returns<string, string, string, string?, int?>((_, _, candidate, _, _) =>
                {
                    lock (_sent) _sent.Add(candidate);
                    return Task.CompletedTask;
                });

            var registry = new Mock<ILobbyRegistry>();
            registry.Setup(r => r.Games).Returns(new ConcurrentDictionary<Guid, GameLobby>());

            _service = new WebRTCSessionService(
                _signaling.Object,
                registry.Object,
                new Mock<IWebRTCApiDispatcher>().Object,
                _central.Object,
                new Mock<IServiceScopeFactory>().Object,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
                _factory,
                NullLogger<WebRTCSessionService>.Instance);
        }

        private Task OfferAsync(string sdp = "offer-sdp")
            => _onOffer!(new WebRTCSignalArgs(_game, "socket-1", sdp, "user-1", "Alice"));

        [Fact]
        public async Task Offer_IsSetOnThePeer_AndItsAnswerIsSent()
        {
            _factory.AnswerSdp = "answer-sdp";

            await OfferAsync("offer-sdp");

            var peer = Assert.Single(_factory.Created);
            Assert.Equal("offer-sdp", peer.RemoteOffer);
            Assert.Equal(new[] { ("socket-1", "answer-sdp") }, _answers);
        }

        [Fact]
        public async Task RejectedOffer_ClosesThePeer_AndSendsNoAnswer()
        {
            _factory.RejectOffer = "bad sdp";

            await OfferAsync();

            Assert.True(Assert.Single(_factory.Created).Closed);
            Assert.Empty(_answers);
        }

        [Fact]
        public async Task LocalCandidatesGatheredBeforeTheAnswer_AreSentAfterIt()
        {
            _factory.CandidatesDuringAnswer = new[] { "candidate:1 1 udp 1 10.0.0.1 5000 typ host" };

            await OfferAsync();

            Assert.Equal(new[] { "answer", "candidate:1 1 udp 1 10.0.0.1 5000 typ host" }, _sent);
        }

        [Fact]
        public async Task RemoteCandidate_ReachesThePeerAsTheBrowserSentIt()
        {
            // Stripping the "candidate:" prefix is a SIPSorcery need, so its adapter does it.
            await OfferAsync();

            await _onIce!(new IceCandidateArgs(_game, "socket-1", "candidate:2 1 udp 1 10.0.0.2 6000 typ host", "0", 0, "user-1"));

            var candidate = Assert.Single(Assert.Single(_factory.Created).RemoteCandidates);
            Assert.Equal("candidate:2 1 udp 1 10.0.0.2 6000 typ host", candidate.Candidate);
            Assert.Equal("0", candidate.SdpMid);
            Assert.Equal(0, candidate.SdpMLineIndex);
        }

        [Fact]
        public async Task TurnCredentialsFromCentral_ReachThePeerFactory()
        {
            _central.Setup(c => c.GetIceServersAsync(It.IsAny<string>())).ReturnsAsync(new IceServersResult
            {
                IceServers = new()
                {
                    new IceServerDto { Urls = new() { "turn:t:3478?transport=udp" }, Username = "123:user", Credential = "secret=" },
                },
                Ttl = 86400,
            });
            await _service.StartSessionAsync(Guid.Parse(_game), "central-session", "token");

            await OfferAsync();

            var turn = Assert.Single(_factory.IceServersUsed.Single());
            Assert.Equal(new IceServer("turn:t:3478?transport=udp", "123:user", "secret="), turn);
        }

        [Fact]
        public async Task FailedPeer_IsClosed()
        {
            await OfferAsync();
            var peer = Assert.Single(_factory.Created);

            peer.RaiseState(PeerState.Failed);

            Assert.True(peer.Closed);
        }

        private sealed class FakePeerFactory : IPeerConnectionFactory
        {
            public string AnswerSdp { get; set; } = "answer";
            public string? RejectOffer { get; set; }
            public string[] CandidatesDuringAnswer { get; set; } = Array.Empty<string>();
            public List<FakePeer> Created { get; } = new();
            public List<IReadOnlyList<IceServer>> IceServersUsed { get; } = new();

            public IPeerConnection Create(IReadOnlyList<IceServer> iceServers)
            {
                IceServersUsed.Add(iceServers);
                var peer = new FakePeer(this);
                Created.Add(peer);
                return peer;
            }
        }

        private sealed class FakePeer : IPeerConnection
        {
            private readonly FakePeerFactory _factory;

            public FakePeer(FakePeerFactory factory) => _factory = factory;

            public string? RemoteOffer { get; private set; }
            public bool Closed { get; private set; }
            public List<IceCandidate> RemoteCandidates { get; } = new();

            public PeerState State { get; private set; } = PeerState.New;
            public bool HasRemoteDescription => RemoteOffer != null;

            public event Action<PeerState>? StateChanged;
            public event Action<IPeerDataChannel>? DataChannel;
            public event Action<IceCandidate>? LocalCandidate;

            public Task<string?> SetRemoteOfferAsync(string sdp)
            {
                if (_factory.RejectOffer != null)
                    return Task.FromResult<string?>(_factory.RejectOffer);
                RemoteOffer = sdp;
                return Task.FromResult<string?>(null);
            }

            public Task<string> CreateAnswerAsync()
            {
                foreach (var candidate in _factory.CandidatesDuringAnswer)
                    LocalCandidate?.Invoke(new IceCandidate(candidate, "0", 0));
                return Task.FromResult(_factory.AnswerSdp);
            }

            public void AddRemoteCandidate(IceCandidate candidate) => RemoteCandidates.Add(candidate);

            public void Close()
            {
                Closed = true;
                State = PeerState.Closed;
            }

            public void RaiseState(PeerState state)
            {
                State = state;
                StateChanged?.Invoke(state);
            }

            // Not raised by these tests; declared for the interface.
            public void RaiseDataChannel(IPeerDataChannel channel) => DataChannel?.Invoke(channel);
        }
    }
}

using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // The browser-wasm GM host's IPeerConnection: the browser's own RTCPeerConnection, reached
    // through IBrowserRtc (JS interop, by string id) and called back through BrowserRtcRouter.
    // A fake IBrowserRtc stands in for the JS side.
    public class BrowserPeerConnectionTests
    {
        private readonly FakeBrowserRtc _rtc = new();
        private readonly BrowserRtcRouter _router = new();
        private readonly BrowserPeerConnectionFactory _factory;

        public BrowserPeerConnectionTests() => _factory = new BrowserPeerConnectionFactory(_rtc, _router);

        private (IPeerConnection Peer, string Id) Create(params IceServer[] servers)
        {
            var peer = _factory.Create(servers);
            return (peer, _rtc.Created.Last().Id);
        }

        private IPeerDataChannel OpenChannel(string peerId, IPeerConnection peer, bool open = true)
        {
            IPeerDataChannel? channel = null;
            peer.DataChannel += c => channel = c;
            _router.OnDataChannel(peerId, "dc-1", open);
            return channel!;
        }

        [Fact]
        public void Create_GivesTheBrowserTheIceServers_WithTurnCredentials()
        {
            Create(new IceServer("stun:s:19302"), new IceServer("turn:t:3478?transport=udp", "123:user", "secret="));

            var servers = JsonDocument.Parse(_rtc.Created.Single().IceServersJson).RootElement.EnumerateArray().ToList();
            Assert.Equal("stun:s:19302", servers[0].GetProperty("urls").GetString());
            Assert.False(servers[0].TryGetProperty("username", out _));
            Assert.Equal("turn:t:3478?transport=udp", servers[1].GetProperty("urls").GetString());
            Assert.Equal("123:user", servers[1].GetProperty("username").GetString());
            Assert.Equal("secret=", servers[1].GetProperty("credential").GetString());
        }

        [Fact]
        public void Create_GivesEveryPeerItsOwnId()
        {
            Create();
            Create();

            Assert.Equal(2, _rtc.Created.Select(c => c.Id).Distinct().Count());
        }

        [Fact]
        public async Task AcceptedOffer_MarksTheRemoteDescription()
        {
            var (peer, id) = Create();

            Assert.Null(await peer.SetRemoteOfferAsync("offer-sdp"));

            Assert.True(peer.HasRemoteDescription);
            Assert.Equal((id, "offer-sdp"), _rtc.Offers.Single());
        }

        [Fact]
        public async Task RejectedOffer_GivesTheBrowsersReason_AndNoRemoteDescription()
        {
            var (peer, _) = Create();
            _rtc.RejectOffer = "InvalidAccessError: bad sdp";

            Assert.Equal("InvalidAccessError: bad sdp", await peer.SetRemoteOfferAsync("offer-sdp"));
            Assert.False(peer.HasRemoteDescription);
        }

        [Fact]
        public async Task CreateAnswer_IsTheBrowsersAnswer()
        {
            var (peer, id) = Create();
            _rtc.AnswerSdp = "answer-sdp";

            Assert.Equal("answer-sdp", await peer.CreateAnswerAsync());
            Assert.Equal(id, _rtc.Answered.Single());
        }

        [Fact]
        public async Task RemoteCandidate_GoesToTheBrowserAsSent_WithMinusOneForNoIndex()
        {
            var (peer, id) = Create();
            await peer.SetRemoteOfferAsync("offer-sdp");

            peer.AddRemoteCandidate(new IceCandidate("candidate:1 1 udp 1 10.0.0.1 5000 typ host", "0", 0));
            peer.AddRemoteCandidate(new IceCandidate("candidate:2 1 udp 1 10.0.0.2 5000 typ host", "0", null));

            Assert.Equal(new[]
            {
                (id, "candidate:1 1 udp 1 10.0.0.1 5000 typ host", (string?)"0", 0),
                (id, "candidate:2 1 udp 1 10.0.0.2 5000 typ host", (string?)"0", -1),
            }, _rtc.RemoteCandidates);
        }

        // The browser's setRemoteDescription is a Promise (SIPSorcery's is synchronous): a
        // candidate arriving while the offer is still being set would fail and be lost.
        [Fact]
        public async Task RemoteCandidate_BeforeTheOfferIsSet_WaitsForIt()
        {
            var (peer, id) = Create();
            var offerSet = new TaskCompletionSource<string?>();
            _rtc.PendingOffer = offerSet.Task;

            var setting = peer.SetRemoteOfferAsync("offer-sdp");
            peer.AddRemoteCandidate(new IceCandidate("candidate:early", "0", 0));
            Assert.Empty(_rtc.RemoteCandidates);

            offerSet.SetResult(null);
            await setting;
            peer.AddRemoteCandidate(new IceCandidate("candidate:late", "0", 0));

            Assert.Equal(new[] { "candidate:early", "candidate:late" }, _rtc.RemoteCandidates.Select(c => c.Item2));
        }

        [Fact]
        public async Task RemoteCandidate_ForARejectedOffer_IsDropped()
        {
            var (peer, _) = Create();
            peer.AddRemoteCandidate(new IceCandidate("candidate:early", "0", 0));
            _rtc.RejectOffer = "bad sdp";

            await peer.SetRemoteOfferAsync("offer-sdp");

            Assert.Empty(_rtc.RemoteCandidates);
        }

        [Theory]
        [InlineData("new", PeerState.New)]
        [InlineData("connecting", PeerState.Connecting)]
        [InlineData("connected", PeerState.Connected)]
        [InlineData("disconnected", PeerState.Disconnected)]
        [InlineData("failed", PeerState.Failed)]
        [InlineData("closed", PeerState.Closed)]
        public void BrowserState_IsRaised_AndKept(string browserState, PeerState expected)
        {
            var (peer, id) = Create();
            var raised = new List<PeerState>();
            peer.StateChanged += raised.Add;

            _router.OnStateChanged(id, browserState);

            Assert.Equal(new[] { expected }, raised);
            Assert.Equal(expected, peer.State);
        }

        [Fact]
        public void UnknownBrowserState_IsIgnored()
        {
            var (peer, id) = Create();
            var raised = new List<PeerState>();
            peer.StateChanged += raised.Add;

            _router.OnStateChanged(id, "something-new");

            Assert.Empty(raised);
            Assert.Equal(PeerState.New, peer.State);
        }

        [Fact]
        public void LocalCandidate_IsRaised_ButNotTheEndOfGathering()
        {
            var (peer, id) = Create();
            var raised = new List<IceCandidate>();
            peer.LocalCandidate += raised.Add;

            _router.OnIceCandidate(id, "candidate:3 1 udp 1 10.0.0.3 7000 typ host", "0", 0);
            _router.OnIceCandidate(id, null, null, -1);
            _router.OnIceCandidate(id, "", "0", 0);

            Assert.Equal(new[] { new IceCandidate("candidate:3 1 udp 1 10.0.0.3 7000 typ host", "0", 0) }, raised);
        }

        [Fact]
        public void DataChannel_OpenedLater_RaisesOpenedOnce_AndIsOpen()
        {
            var (peer, id) = Create();
            var channel = OpenChannel(id, peer, open: false);
            var opened = 0;
            channel.Opened += () => opened++;
            Assert.False(channel.IsOpen);

            _router.OnChannelOpen("dc-1");
            _router.OnChannelOpen("dc-1");

            Assert.Equal(1, opened);
            Assert.True(channel.IsOpen);
        }

        [Fact]
        public void DataChannel_AlreadyOpenWhenAnnounced_IsOpen()
        {
            // The service checks IsOpen right after subscribing, for a channel open before then.
            var (peer, id) = Create();

            Assert.True(OpenChannel(id, peer).IsOpen);
        }

        [Fact]
        public void DataChannel_Message_ArrivesAsUtf8()
        {
            var (peer, id) = Create();
            var channel = OpenChannel(id, peer);
            byte[]? received = null;
            channel.Message += data => received = data;

            _router.OnChannelMessage("dc-1", "{\"command\":\"zażółć\"}");

            Assert.Equal("{\"command\":\"zażółć\"}", Encoding.UTF8.GetString(received!));
        }

        [Fact]
        public void DataChannel_SendAndBufferedAmount_GoToTheBrowser()
        {
            var (peer, id) = Create();
            var channel = OpenChannel(id, peer);
            _rtc.Buffered = 2048;

            channel.Send("hello");

            Assert.Equal(("dc-1", "hello"), _rtc.Sent.Single());
            Assert.Equal(2048UL, channel.BufferedAmount);
        }

        [Fact]
        public void DataChannel_Closed_IsRaised_AndNotOpen()
        {
            var (peer, id) = Create();
            var channel = OpenChannel(id, peer);
            var closed = 0;
            channel.Closed += () => closed++;

            _router.OnChannelClose("dc-1");

            Assert.Equal(1, closed);
            Assert.False(channel.IsOpen);
        }

        [Fact]
        public void Close_ClosesTheBrowserPeer_AndItsChannels_AndIgnoresItsLaterEvents()
        {
            var (peer, id) = Create();
            var channel = OpenChannel(id, peer);
            var closed = 0;
            channel.Closed += () => closed++;
            var states = new List<PeerState>();
            peer.StateChanged += states.Add;

            peer.Close();
            _router.OnStateChanged(id, "connected");
            _router.OnChannelClose("dc-1");

            Assert.Equal(id, _rtc.ClosedPeers.Single());
            Assert.Equal(PeerState.Closed, peer.State);
            Assert.Equal(1, closed);
            Assert.False(channel.IsOpen);
            Assert.Empty(states);
        }

        [Fact]
        public void EventsForUnknownIds_AreIgnored()
        {
            _router.OnStateChanged("pc-nope", "connected");
            _router.OnDataChannel("pc-nope", "dc-nope", true);
            _router.OnIceCandidate("pc-nope", "candidate:x", "0", 0);
            _router.OnChannelOpen("dc-nope");
            _router.OnChannelMessage("dc-nope", "x");
            _router.OnChannelClose("dc-nope");
        }

        private sealed class FakeBrowserRtc : IBrowserRtc
        {
            public List<(string Id, string IceServersJson)> Created { get; } = new();
            public List<(string, string)> Offers { get; } = new();
            public List<string> Answered { get; } = new();
            public List<(string, string, string?, int)> RemoteCandidates { get; } = new();
            public List<string> ClosedPeers { get; } = new();
            public List<(string, string)> Sent { get; } = new();
            public string? RejectOffer { get; set; }
            public Task<string?>? PendingOffer { get; set; }
            public string AnswerSdp { get; set; } = "answer";
            public double Buffered { get; set; }

            public void CreatePeer(string peerId, string iceServersJson) => Created.Add((peerId, iceServersJson));

            public Task<string?> SetRemoteOfferAsync(string peerId, string sdp)
            {
                if (PendingOffer != null)
                {
                    Offers.Add((peerId, sdp));
                    return PendingOffer;
                }
                if (RejectOffer != null)
                    return Task.FromResult<string?>(RejectOffer);
                Offers.Add((peerId, sdp));
                return Task.FromResult<string?>(null);
            }

            public Task<string> CreateAnswerAsync(string peerId)
            {
                Answered.Add(peerId);
                return Task.FromResult(AnswerSdp);
            }

            public void AddIceCandidate(string peerId, string candidate, string? sdpMid, int sdpMLineIndex)
                => RemoteCandidates.Add((peerId, candidate, sdpMid, sdpMLineIndex));

            public void ClosePeer(string peerId) => ClosedPeers.Add(peerId);

            public void Send(string channelId, string message) => Sent.Add((channelId, message));

            public double BufferedAmount(string channelId) => Buffered;
        }
    }
}

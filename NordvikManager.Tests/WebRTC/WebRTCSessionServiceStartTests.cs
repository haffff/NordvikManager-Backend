using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebRTC;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace NordvikManager.Tests.WebRTC
{
    // Signaling connects with Central's GM signaling ticket, which outlives the 15-minute
    // access token, so the socket can re-authenticate when it reconnects on its own.
    public class WebRTCSessionServiceStartTests
    {
        private readonly Mock<ISignalingService> _signaling = new();
        private readonly Mock<ICentralServerService> _central = new();

        private WebRTCSessionService Create() => new(
            _signaling.Object,
            new Mock<ILobbyRegistry>().Object,
            new Mock<IWebRTCApiDispatcher>().Object,
            _central.Object,
            new Mock<IServiceScopeFactory>().Object,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            NullLogger<WebRTCSessionService>.Instance);

        [Fact]
        public async Task StartSession_ConnectsWithTheSignalingTicket()
        {
            var gameId = Guid.NewGuid();
            _central.Setup(c => c.GetSignalingTicketAsync("access", "session")).ReturnsAsync("ticket");

            await Create().StartSessionAsync(gameId, "session", "access");

            _signaling.Verify(s => s.ConnectAsync(gameId.ToString(), "session", "ticket"));
        }

        [Fact]
        public async Task StartSession_WithoutATicket_UsesTheAccessToken()
        {
            // An older Central without the ticket route.
            var gameId = Guid.NewGuid();
            _central.Setup(c => c.GetSignalingTicketAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync((string?)null);

            await Create().StartSessionAsync(gameId, "session", "access");

            _signaling.Verify(s => s.ConnectAsync(gameId.ToString(), "session", "access"));
        }
    }
}

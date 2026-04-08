using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

// Alias avoids collision with the SocketIO namespace brought in by some transitive dependencies.
using SioClient = SocketIOClient.SocketIO;
using SioOptions = SocketIOClient.SocketIOOptions;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Maintains one Socket.io client per active game session, connected to the Central Server.
    /// Relays WebRTC signaling events (offer, ICE candidates) between browser peers and the GM Backend.
    ///
    /// Central Server protocol:
    ///   authenticate → { token, sessionId, role: "gm" }
    ///   authenticated ← { peerId }
    ///   peer-joined   ← { peerId, userId, username, role }
    ///   peer-left     ← { peerId, username }
    ///   webrtc-offer  ← { fromPeerId, offer: { type, sdp } }
    ///   webrtc-answer → { targetPeerId, answer: { type, sdp } }
    ///   ice-candidate ↔ { targetPeerId/fromPeerId, candidate }
    /// </summary>
    public class SignalingService : ISignalingService
    {
        private readonly string _centralServerUrl;
        private readonly ILogger<SignalingService> _logger;
        private readonly ConcurrentDictionary<string, SioClient> _clients = new();
        // gameId → centralSessionId (needed to authenticate)
        private readonly ConcurrentDictionary<string, string> _sessionIds = new();
        // gameId is present here only after the Central Server sends back 'authenticated'
        private readonly ConcurrentDictionary<string, bool> _authenticated = new();

        public event Func<PeerJoinedArgs, Task>? PeerJoined;
        public event Func<PeerLeftArgs, Task>? PeerLeft;
        public event Func<WebRTCSignalArgs, Task>? OfferReceived;
        public event Func<IceCandidateArgs, Task>? IceCandidateReceived;

        public SignalingService(IConfiguration configuration, ILogger<SignalingService> logger)
        {
            _centralServerUrl = configuration["CentralServerUrl"]?.TrimEnd('/') ?? "http://localhost:3000";
            _logger = logger;
        }

        public bool IsConnected(string gameId)
            => _clients.TryGetValue(gameId, out var client) && client.Connected && _authenticated.ContainsKey(gameId);

        public async Task ConnectAsync(string gameId, string centralSessionId, string centralToken)
        {
            if (_clients.TryGetValue(gameId, out var existing))
            {
                if (existing.Connected && _authenticated.ContainsKey(gameId))
                    return;

                // Stale: socket connected but not authenticated (e.g. auth-error, server restart),
                // or fully disconnected — clean up before reconnecting.
                _clients.TryRemove(gameId, out _);
                _authenticated.TryRemove(gameId, out _);
                try { await existing.DisconnectAsync(); } catch { /* best-effort */ }
            }

            _sessionIds[gameId] = centralSessionId;

            var client = new SioClient(_centralServerUrl, new SioOptions
            {
                ExtraHeaders = new Dictionary<string, string>
                {
                    ["Cookie"] = $"Authorization={centralToken}"
                }
            });

            // On connected, authenticate as GM for the session
            client.OnConnected += async (sender, e) =>
            {
                _logger.LogInformation("Signaling connected to Central Server for game {GameId}", gameId);
                await client.EmitAsync("authenticate", new
                {
                    token = centralToken,
                    sessionId = centralSessionId,
                    role = "gm"
                });
            };

            client.OnDisconnected += (sender, reason) =>
            {
                _logger.LogWarning("Signaling disconnected from Central Server for game {GameId}: {Reason}", gameId, reason);
                _authenticated.TryRemove(gameId, out _);
            };

            // authenticated: Central Server confirms GM registration — mark as truly connected
            client.On("authenticated", _ =>
            {
                _logger.LogInformation("Signaling authenticated with Central Server for game {GameId}", gameId);
                _authenticated[gameId] = true;
            });

            // auth-error: Central Server rejected the authenticate event
            client.On("auth-error", response =>
            {
                try
                {
                    var msg = response.GetValue<AuthErrorPayload>().error;
                    _logger.LogError("Signaling auth-error for game {GameId}: {Error}", gameId, msg);
                }
                catch
                {
                    _logger.LogError("Signaling auth-error for game {GameId}", gameId);
                }
                _authenticated.TryRemove(gameId, out _);
            });

            // peer-joined: { peerId, userId, username, role }
            client.On("peer-joined", response =>
            {
                _logger.LogInformation("peer-joined received for game {GameId}: {Raw}", gameId, response);
                try
                {
                    var payload = response.GetValue<PeerJoinedPayload>();
                    _ = PeerJoined?.Invoke(new PeerJoinedArgs(gameId, payload.userId, payload.username, payload.peerId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling peer-joined event for game {GameId}", gameId);
                }
            });

            // peer-left: { peerId, username }
            client.On("peer-left", response =>
            {
                try
                {
                    var peerId = response.GetValue<PeerLeftPayload>().peerId;
                    _ = PeerLeft?.Invoke(new PeerLeftArgs(gameId, peerId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling peer-left event for game {GameId}", gameId);
                }
            });

            // webrtc-offer: { fromPeerId, offer: { type, sdp }, userId?, username? }
            client.On("webrtc-offer", response =>
            {
                _logger.LogInformation("webrtc-offer received for game {GameId}: {Raw}", gameId, response);
                try
                {
                    var payload = response.GetValue<WebRTCOfferPayload>();
                    _ = OfferReceived?.Invoke(new WebRTCSignalArgs(gameId, payload.fromPeerId, payload.offer.sdp, payload.userId, payload.username));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling webrtc-offer event for game {GameId}", gameId);
                }
            });

            // ping-gm uses Socket.IO ACK — Central Server calls socket.timeout().emit(PING_GM, ackCallback).
            // Must call response.CallbackAsync() to trigger the ack; emitting a separate event does nothing.
            client.On("ping-gm", async response =>
            {
                _logger.LogDebug("ping-gm received for game {GameId} — sending ack", gameId);
                await response.CallbackAsync();
            });

            // ice-candidate: { fromPeerId, candidate: { candidate, sdpMid, sdpMLineIndex, usernameFragment }, userId? }
            client.On("ice-candidate", response =>
            {
                try
                {
                    var payload = response.GetValue<IceCandidatePayload>();
                    _ = IceCandidateReceived?.Invoke(new IceCandidateArgs(
                        gameId,
                        payload.fromPeerId,
                        payload.candidate.candidate,
                        payload.candidate.sdpMid,
                        payload.candidate.sdpMLineIndex,
                        payload.userId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling ice-candidate event for game {GameId}", gameId);
                }
            });

            await client.ConnectAsync();
            _clients[gameId] = client;
        }

        public async Task DisconnectAsync(string gameId)
        {
            if (_clients.TryRemove(gameId, out var client))
                await client.DisconnectAsync();
            _sessionIds.TryRemove(gameId, out _);
            _authenticated.TryRemove(gameId, out _);
        }

        /// <summary>Sends a WebRTC answer SDP to a specific browser peer.</summary>
        public async Task SendAnswerAsync(string gameId, string targetPeerId, string sdp)
        {
            if (_clients.TryGetValue(gameId, out var client))
                await client.EmitAsync("webrtc-answer", new
                {
                    targetPeerId,
                    answer = new { type = "answer", sdp }
                });
        }

        /// <summary>Sends an ICE candidate to a specific browser peer.</summary>
        public async Task SendIceCandidateAsync(string gameId, string targetPeerId, string candidate, string? sdpMid, int? sdpMLineIndex)
        {
            if (_clients.TryGetValue(gameId, out var client))
                await client.EmitAsync("ice-candidate", new
                {
                    targetPeerId,
                    candidate = new { candidate, sdpMid, sdpMLineIndex }
                });
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var client in _clients.Values)
                await client.DisconnectAsync();
            _clients.Clear();
        }

        // ── Private payload types (matched to Central Server camelCase JSON) ──

        private record PeerJoinedPayload(string peerId, string userId, string username, string role);
        private record PeerLeftPayload(string peerId, string username);
        private record SdpPayload(string type, string sdp);
        private record WebRTCOfferPayload(string fromPeerId, SdpPayload offer, string? userId = null, string? username = null);
        private record CandidateObject(string candidate, string? sdpMid, int? sdpMLineIndex, string? usernameFragment);
        private record IceCandidatePayload(string fromPeerId, CandidateObject candidate, string? userId = null);
        private record AuthErrorPayload(string error);
    }
}

using DndOnePlaceManager.Application.Commands.BattleMap;
using DndOnePlaceManager.Application.Commands.Game.Player.GetPlayer;
using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.Enums;
using DNDOnePlaceManager.Extensions;
using DNDOnePlaceManager.Services;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Implementations.HookArgs;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebSockets.Core;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIPSorcery.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DNDOnePlaceManager.WebRTC
{
    /// <summary>
    /// Manages RTCPeerConnections for all browser peers in active game sessions.
    ///
    /// Data channel message routing:
    ///   { "type": "api-request", ... } → IWebRTCApiDispatcher (tunneled REST)
    ///   anything else                  → GameLobby.HandleCommand (real-time game events)
    /// </summary>
    public class WebRTCSessionService : IWebRTCSessionService
    {
        private readonly ISignalingService _signaling;
        private readonly ILobbyRegistry _lobbyRegistry;
        private readonly IWebRTCApiDispatcher _apiDispatcher;
        private readonly ICentralServerService _centralServerService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<WebRTCSessionService> _logger;
        // Fallback ICE servers from local config, used when /meta is unreachable
        private readonly List<RTCIceServer> _fallbackIceServers;
        // ICE servers fetched from Central Server /meta, updated on each StartSessionAsync
        private List<RTCIceServer> _iceServers;

        // socketId → RTCPeerConnection
        private readonly ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();
        // socketId → (gameId, userId)
        private readonly ConcurrentDictionary<string, (string GameId, string UserId)> _peerMeta = new();
        // socketId → Unix milliseconds when peer-joined was received, used for connection timing logs
        private readonly ConcurrentDictionary<string, long> _peerConnectStartMs = new();
        // ICE candidates gathered before the answer is sent are buffered here so the frontend
        // always receives the answer before any of our ICE candidates.
        // null value = answer already sent, send candidates directly.
        private readonly ConcurrentDictionary<string, List<(string Candidate, string? SdpMid, int? SdpMLineIndex)>?> _pendingLocalCandidates = new();
        // TCS per socketId — completed when OnPeerJoined finishes creating the RTCPeerConnection.
        // Used by offer/ICE handlers to wait for the connection without polling.
        private readonly ConcurrentDictionary<string, TaskCompletionSource<RTCPeerConnection?>> _peerReadySignals = new();

        public WebRTCSessionService(
            ISignalingService signaling,
            ILobbyRegistry lobbyRegistry,
            IWebRTCApiDispatcher apiDispatcher,
            ICentralServerService centralServerService,
            IServiceScopeFactory serviceScopeFactory,
            IConfiguration configuration,
            ILogger<WebRTCSessionService> logger)
        {
            _signaling = signaling;
            _lobbyRegistry = lobbyRegistry;
            _apiDispatcher = apiDispatcher;
            _centralServerService = centralServerService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;

            _fallbackIceServers = BuildFallbackIceServers(configuration);
            _iceServers = _fallbackIceServers;

            _signaling.PeerJoined += OnPeerJoined;
            _signaling.PeerLeft += OnPeerLeft;
            _signaling.OfferReceived += OnOfferReceived;
            _signaling.IceCandidateReceived += OnIceCandidateReceived;
        }

        public bool IsConnected(Guid gameId)
            => _signaling.IsConnected(gameId.ToString());

        public async Task StartSessionAsync(Guid gameId, string centralSessionId, string centralToken)
        {
            var meta = await _centralServerService.GetMetaAsync();
            if (meta != null)
            {
                _iceServers = BuildIceServersFromMeta(meta);
                _logger.LogInformation(
                    "ICE servers loaded from Central Server /meta: {Count} STUN, TURN={Turn}",
                    meta.StunServers.Count, meta.TurnServer ?? "none");
            }
            else
            {
                _logger.LogWarning("Could not reach Central Server /meta — using fallback ICE config.");
                _iceServers = _fallbackIceServers;
            }

            await _signaling.ConnectAsync(gameId.ToString(), centralSessionId, centralToken);
        }

        public Task StopSessionAsync(Guid gameId)
            => _signaling.DisconnectAsync(gameId.ToString());

        private TaskCompletionSource<RTCPeerConnection?> GetOrCreateReadySignal(string socketId)
            => _peerReadySignals.GetOrAdd(socketId, _ =>
                new TaskCompletionSource<RTCPeerConnection?>(TaskCreationOptions.RunContinuationsAsynchronously));

        private Task OnPeerJoined(PeerJoinedArgs args)
        {
            _peerConnectStartMs[args.SocketId] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            RTCPeerConnection pc;
            try
            {
                var config = new RTCConfiguration { iceServers = _iceServers };
                pc = new RTCPeerConnection(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create RTCPeerConnection for peer {SocketId} in game {GameId}", args.SocketId, args.GameId);
                TryNotifyPlayerWebRTCError(args.GameId, args.UserId, "WebRTC connection could not be established");
                GetOrCreateReadySignal(args.SocketId).TrySetResult(null);
                return Task.CompletedTask;
            }

            _logger.LogInformation("Peer connection created for socketId={SocketId} user={User} game={GameId}",
                args.SocketId, args.Username, args.GameId);

            _peerConnections[args.SocketId] = pc;
            _peerMeta[args.SocketId] = (args.GameId, args.UserId);
            _pendingLocalCandidates[args.SocketId] = new List<(string, string?, int?)>();
            GetOrCreateReadySignal(args.SocketId).TrySetResult(pc);

            pc.onconnectionstatechange += state =>
            {
                _logger.LogInformation("WebRTC peer connection state → {State} for socket {SocketId} in game {GameId}", state, args.SocketId, args.GameId);
                if (state == RTCPeerConnectionState.failed || state == RTCPeerConnectionState.disconnected)
                {
                    TryNotifyPlayerWebRTCError(args.GameId, args.UserId, $"WebRTC connection {state}");
                    CleanupPeer(args.SocketId);
                    RemoveDataChannelConnection(args.GameId, args.UserId);
                }
            };

            pc.ondatachannel += dataChannel =>
            {
                // Shared state — set by HandleOpenAsync, read by onmessage/onclose.
                WebRTCPlayerConnection? connection = null;
                PlayerDTO? playerEntry = null;
                // chunkId → string slots; lives for the lifetime of this data channel.
                var chunkBuffers = new Dictionary<string, string?[]>();

                // onmessage below dispatches lobby.HandleCommand fire-and-forget, so back-to-back
                // commands from this connection (e.g. a tree "Delete All" sending many tree_remove
                // commands in a tight loop) can run concurrently, each reading DB state before the
                // other's write commits. This gate forces HandleCommand calls from THIS connection
                // to run one at a time, in arrival order, without blocking other connections.
                var commandGate = new SemaphoreSlim(1, 1);
                async Task HandleCommandInOrderAsync(GameLobby lobby, PlayerDTO player, string message)
                {
                    await commandGate.WaitAsync();
                    try
                    {
                        await lobby.HandleCommand(player, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling WebRTC command for player {PlayerId}", player.Id);
                    }
                    finally
                    {
                        commandGate.Release();
                    }
                }

                // SIPSorcery fires onopen as a plain Action exactly once.
                // We fire-and-forget an async method so we can hit the DB if needed.
                // The Interlocked guard defends against the rare race where the channel transitions
                // to 'open' between assigning onopen and the readyState check below, which would
                // otherwise call HandleOpenAsync twice and create a duplicate lobby connection.
                var openHandled = 0;
                async Task HandleOpenAsync()
                {
                    if (Interlocked.Exchange(ref openHandled, 1) != 0) return;
                    if (!Guid.TryParse(args.GameId, out var gameId))
                    {
                        SendChannelError(dataChannel, "err: invalid game id");
                        return;
                    }
                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                    {
                        SendChannelError(dataChannel, "err: no lobby found");
                        return;
                    }

                    playerEntry = lobby.ConnectedPlayers.Keys
                        .FirstOrDefault(p => p.CentralServerUserId == args.UserId);

                    if (playerEntry == null)
                    {
                        // Player not yet in lobby (WebRTC opened before or without WebSocket).
                        // Resolve from DB, auto-creating if not found (mirrors AddPlayer flow).
                        using var scope = _serviceScopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        var user = new User { Id = args.UserId };

                        var response = await mediator.Send(new GetPlayerCommand
                        {
                            GameID = gameId,
                            User = user
                        });

                        if (response.Player == null)
                        {
                            // Auto-create the player — same path as joining a game for the first time.
                            // The password gate is skipped here: reaching this point already required
                            // passing the Central Server's join check for this session, and this
                            // fallback has no channel to prompt the player for a password anyway.
                            await mediator.Send(new AddPlayerCommand { GameID = gameId, User = user, SkipPasswordCheck = true });
                            response = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
                        }

                        if (response.Player == null)
                        {
                            _logger.LogWarning(
                                "WebRTC data channel opened for userId={UserId} but player could not be created in game {GameId}",
                                args.UserId, gameId);
                            SendChannelError(dataChannel, "err: no player found");
                            return;
                        }

                        connection = new WebRTCPlayerConnection(dataChannel);
                        playerEntry = await LobbyConnectionHelper.ConnectPlayerAsync(lobby, response.Player, connection);
                    }
                    else
                    {
                        connection = new WebRTCPlayerConnection(dataChannel);
                        lobby.ConnectedPlayers[playerEntry].Add(connection);
                    }

                    var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        - (_peerConnectStartMs.TryGetValue(args.SocketId, out var t) ? t : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    _logger.LogInformation(
                        "Data channel open player={Name} game={GameId} socketId={SocketId} elapsed={Elapsed}ms",
                        playerEntry.Name, gameId, args.SocketId, elapsed);
                }

                dataChannel.onopen += () => _ = HandleOpenAsync();

                // SIPSorcery fires onopen?.Invoke() exactly once at transition time.
                // If the channel was already open before we assigned the handler (race between
                // ondatachannel dispatch and the open transition), call it manually now.
                if (dataChannel.readyState == RTCDataChannelState.open)
                    _ = HandleOpenAsync();

                dataChannel.onclose += () =>
                    RemoveDataChannelConnection(args.GameId, args.UserId);

                dataChannel.onmessage += (_, _, data) =>
                {
                    if (connection == null || playerEntry == null)
                        return;
                    if (!Guid.TryParse(args.GameId, out var gameId))
                        return;

                    var text = Encoding.UTF8.GetString(data);

                    // Reassemble chunked messages before any other processing.
                    if (TryParseChunk(text, out var chunk))
                    {
                        text = TryAssembleChunk(chunkBuffers, chunk!);
                        if (text == null)
                            return; // waiting for remaining chunks
                    }

                    // Check if this is a tunneled REST request
                    if (TryParseApiRequest(text, out var apiRequest))
                    {
                        _ = _apiDispatcher.DispatchAsync(apiRequest!, playerEntry, gameId, connection);
                        return;
                    }

                    // Otherwise route as a real-time game command
                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        _ = HandleCommandInOrderAsync(lobby, playerEntry, text);
                };
            };

            pc.onicecandidate += candidate =>
            {
                if (candidate == null)
                    return;

                // If _pendingLocalCandidates still has a list for this socket, the answer has not
                // been sent yet — buffer the candidate. Once the answer is sent the list is replaced
                // with null and candidates are forwarded directly.
                if (_pendingLocalCandidates.TryGetValue(args.SocketId, out var pending) && pending != null)
                {
                    lock (pending)
                        pending.Add((candidate.candidate, candidate.sdpMid, (int?)candidate.sdpMLineIndex));
                }
                else
                {
                    _ = _signaling.SendIceCandidateAsync(
                        args.GameId,
                        args.SocketId,
                        candidate.candidate,
                        candidate.sdpMid,
                        (int?)candidate.sdpMLineIndex);
                }
            };

            return Task.CompletedTask;
        }

        private async Task OnOfferReceived(WebRTCSignalArgs args)
        {
            _logger.LogDebug("Processing WebRTC offer from socketId={SocketId} game={GameId}", args.FromSocketId, args.GameId);

            // peer-joined and webrtc-offer are dispatched concurrently on the thread pool by SocketIOClient.
            // Wait up to 2 s for the peer connection that OnPeerJoined is creating in parallel.
            var pc = await WaitForPeerConnectionAsync(args.FromSocketId);

            if (pc == null)
            {
                _logger.LogWarning(
                    "WebRTC offer received from unknown peer {SocketId} in game {GameId} — no peer connection after waiting",
                    args.FromSocketId, args.GameId);
                return;
            }

            try
            {
                var descResult = pc.setRemoteDescription(new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.offer,
                    sdp = args.Sdp
                });

                if (descResult != SetDescriptionResultEnum.OK)
                {
                    _logger.LogError("setRemoteDescription failed: {Result} for peer {SocketId} in game {GameId}", descResult, args.FromSocketId, args.GameId);
                    if (_peerMeta.TryGetValue(args.FromSocketId, out var metaErr))
                        TryNotifyPlayerWebRTCError(metaErr.GameId, metaErr.UserId, "WebRTC negotiation failed");
                    CleanupPeer(args.FromSocketId);
                    return;
                }

                var answer = pc.createAnswer(null);

                await pc.setLocalDescription(answer);

                // Send the answer and then immediately flush any ICE candidates that were buffered
                // during setLocalDescription. This guarantees the frontend always receives the answer
                // before our ICE candidates, so addIceCandidate never fails with "remote description was null".
                await _signaling.SendAnswerAsync(args.GameId, args.FromSocketId, answer.sdp);
                _logger.LogInformation("WebRTC answer sent to socketId={SocketId} game={GameId}",
                    args.FromSocketId, args.GameId);

                await FlushPendingLocalCandidatesAsync(args.GameId, args.FromSocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process WebRTC offer from peer {SocketId} in game {GameId}", args.FromSocketId, args.GameId);
                if (_peerMeta.TryGetValue(args.FromSocketId, out var meta))
                    TryNotifyPlayerWebRTCError(meta.GameId, meta.UserId, "WebRTC negotiation failed");
                CleanupPeer(args.FromSocketId);
            }
        }

        private async Task OnIceCandidateReceived(IceCandidateArgs args)
        {
            var pc = await WaitForPeerConnectionAsync(args.FromSocketId);

            if (pc == null)
            {
                _logger.LogWarning(
                    "ICE candidate received for unknown peer {SocketId} in game {GameId} — no peer connection after waiting",
                    args.FromSocketId, args.GameId);
                if (_peerMeta.TryGetValue(args.FromSocketId, out var meta))
                {
                    TryNotifyPlayerWebRTCError(meta.GameId, meta.UserId, "WebRTC connection failed: no peer session found");
                    CleanupPeer(args.FromSocketId);
                }
                return;
            }

            // Browsers send candidates with the "candidate:" prefix. SIPSorcery expects the raw
            // candidate string WITHOUT that prefix (it adds its own "a=candidate:" when parsing).
            var candidateStr = args.Candidate ?? string.Empty;
            if (candidateStr.StartsWith("candidate:", StringComparison.OrdinalIgnoreCase))
                candidateStr = candidateStr.Substring("candidate:".Length);

            _logger.LogInformation(
                "ICE candidate received from peer {SocketId} in game {GameId}: sdpMid={SdpMid} index={SdpMLineIndex} candidate={Candidate}",
                args.FromSocketId, args.GameId, args.SdpMid, args.SdpMLineIndex, candidateStr);

            try
            {
                pc.addIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidateStr,
                    sdpMid = args.SdpMid,
                    sdpMLineIndex = args.SdpMLineIndex.HasValue ? (ushort)args.SdpMLineIndex.Value : (ushort)0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "addIceCandidate threw for peer {SocketId} in game {GameId}", args.FromSocketId, args.GameId);
            }
        }

        private Task OnPeerLeft(PeerLeftArgs args)
        {
            CleanupPeer(args.SocketId);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Waits for <see cref="OnPeerJoined"/> to finish creating the RTCPeerConnection.
        /// Uses a TaskCompletionSource so there is no polling and no fixed timeout —
        /// the first RTCPeerConnection creation can take several seconds (SIPSorcery loads
        /// BouncyCastle and scans network interfaces on first use).
        /// Falls back to a 15-second hard timeout to avoid waiting forever if peer-joined never arrives.
        /// </summary>
        private async Task<RTCPeerConnection?> WaitForPeerConnectionAsync(string socketId, int timeoutMs = 15000)
        {
            if (_peerConnections.TryGetValue(socketId, out var existing))
                return existing;

            var tcs = GetOrCreateReadySignal(socketId);
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private async Task FlushPendingLocalCandidatesAsync(string gameId, string socketId)
        {
            // Atomically swap the buffer out for null so the onicecandidate callback
            // switches to sending directly from this point on.
            if (!_pendingLocalCandidates.TryRemove(socketId, out var buffered) || buffered == null)
                return;

            List<(string Candidate, string? SdpMid, int? SdpMLineIndex)> snapshot;
            lock (buffered)
                snapshot = new List<(string, string?, int?)>(buffered);

            foreach (var (candidate, sdpMid, sdpMLineIndex) in snapshot)
                await _signaling.SendIceCandidateAsync(gameId, socketId, candidate, sdpMid, sdpMLineIndex);

            // Re-insert null so subsequent onicecandidate fires are sent directly.
            _pendingLocalCandidates[socketId] = null;
        }

        private void CleanupPeer(string socketId)
        {
            _logger.LogInformation("Cleaning up peer socketId={SocketId}", socketId);
            if (_peerConnections.TryRemove(socketId, out var pc))
                pc.close();
            _peerMeta.TryRemove(socketId, out _);
            _pendingLocalCandidates.TryRemove(socketId, out _);
            _peerConnectStartMs.TryRemove(socketId, out _);
            if (_peerReadySignals.TryRemove(socketId, out var tcs))
                tcs.TrySetResult(null); // unblock any waiter that hasn't timed out yet
        }

        private void TryNotifyPlayerWebRTCError(string gameIdStr, string userId, string message)
        {
            if (!Guid.TryParse(gameIdStr, out var gameId))
                return;
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            var player = lobby.ConnectedPlayers.Keys
                .FirstOrDefault(p => p.CentralServerUserId == userId);
            if (player == null)
                return;

            _ = lobby.ConnectedPlayers[player].SendMessageToPlayer(new
            {
                command = WebSocketCommandNames.ErrorGeneric,
                result = message,
                onlyToSender = true
            });
        }

        private void RemoveDataChannelConnection(string gameIdStr, string userId)
        {
            if (!Guid.TryParse(gameIdStr, out var gameId))
                return;
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return;

            var playerEntry = lobby.ConnectedPlayers.Keys
                .FirstOrDefault(p => p.CentralServerUserId == userId);

            if (playerEntry == null)
                return;

            var toRemove = lobby.ConnectedPlayers[playerEntry]
                .OfType<WebRTCPlayerConnection>()
                .FirstOrDefault();

            if (toRemove != null)
                _ = LobbyConnectionHelper.DisconnectPlayerAsync(lobby, playerEntry, toRemove);
        }

        private static List<RTCIceServer> BuildFallbackIceServers(IConfiguration configuration)
        {
            var servers = new List<RTCIceServer>();

            var stunUrls = configuration.GetSection("WebRTC:StunServers").Get<string[]>();
            if (stunUrls?.Length > 0)
            {
                foreach (var url in stunUrls)
                    servers.Add(new RTCIceServer { urls = url });
            }
            else
            {
                // Legacy single-value fallback
                var single = configuration["WebRTC:StunServer"];
                servers.Add(new RTCIceServer { urls = single ?? "stun:stun.l.google.com:19302" });
            }

            var turnUrl = configuration["WebRTC:TurnServer"];
            if (!string.IsNullOrEmpty(turnUrl))
                servers.Add(new RTCIceServer { urls = turnUrl });

            return servers;
        }

        private static List<RTCIceServer> BuildIceServersFromMeta(CentralServerMeta meta)
        {
            var servers = new List<RTCIceServer>();

            foreach (var url in meta.StunServers)
                servers.Add(new RTCIceServer { urls = url });

            if (!string.IsNullOrEmpty(meta.TurnServer))
                servers.Add(new RTCIceServer { urls = meta.TurnServer });

            // If meta returned nothing, fall back to Google STUN
            if (servers.Count == 0)
                servers.Add(new RTCIceServer { urls = "stun:stun.l.google.com:19302" });

            return servers;
        }

        private static void SendChannelError(RTCDataChannel dc, string message)
        {
            if (dc.readyState == RTCDataChannelState.open)
                dc.send(JsonConvert.SerializeObject(
                    new { command = WebSocketCommandNames.ErrorGeneric, data = message }));
        }

        private static bool TryParseApiRequest(string text, out WebRTCApiRequest? request)
        {
            request = null;
            if (string.IsNullOrWhiteSpace(text) || text[0] != '{')
                return false;
            try
            {
                var obj = JObject.Parse(text);
                if (obj["type"]?.Value<string>() != "api-request")
                    return false;
                request = obj.ToObject<WebRTCApiRequest>();
                return request != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseChunk(string text, out ChunkEnvelope? chunk)
        {
            chunk = null;
            if (string.IsNullOrWhiteSpace(text) || text[0] != '{')
                return false;
            try
            {
                var obj = JObject.Parse(text);
                if (obj["type"]?.Value<string>() != "chunk")
                    return false;
                var chunkId = obj["chunkId"]?.Value<string>();
                var index = obj["index"]?.Value<int>();
                var total = obj["total"]?.Value<int>();
                var data = obj["data"]?.Value<string>();
                if (string.IsNullOrEmpty(chunkId) || index == null || total is null or <= 0 || data == null)
                    return false;
                chunk = new ChunkEnvelope(chunkId, index.Value, total.Value, data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Stores a chunk slice and returns the fully reassembled string when all chunks have arrived,
        /// or null if more chunks are still expected.
        /// </summary>
        private static string? TryAssembleChunk(Dictionary<string, string?[]> buffers, ChunkEnvelope chunk)
        {
            if (!buffers.TryGetValue(chunk.ChunkId, out var parts))
            {
                parts = new string?[chunk.Total];
                buffers[chunk.ChunkId] = parts;
            }

            parts[chunk.Index] = chunk.Data;

            if (parts.Any(p => p == null))
                return null;

            buffers.Remove(chunk.ChunkId);
            return string.Concat(parts);
        }

        private record ChunkEnvelope(string ChunkId, int Index, int Total, string Data);
    }
}

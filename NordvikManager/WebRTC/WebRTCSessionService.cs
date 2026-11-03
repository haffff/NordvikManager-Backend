using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Application.Commands.Player.CheckUserBanned;
using NordvikManager.Application.DataTransferObjects.Game;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Enums;
using NordvikManager.Extensions;
using NordvikManager.Services;
using NordvikManager.Services.Implementations;
using NordvikManager.Services.Implementations.HookArgs;
using NordvikManager.Services.Interfaces;
using NordvikManager.WebSockets.Core;
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

namespace NordvikManager.WebRTC
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
        // Fallback STUN servers from local config, used when Central's /api/ice-servers is unreachable
        private readonly List<RTCIceServer> _fallbackIceServers;
        // ICE servers (incl. TURN credentials) fetched from Central, updated on each StartSessionAsync
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
        // The lobby connection each peer opened, so a peer going away removes exactly that one.
        private readonly PeerLobbyConnections _lobbyConnections;

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

            _lobbyConnections = new PeerLobbyConnections(RemoveDataChannelConnectionAsync);

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
            // TURN credentials last TURN_TTL_SECONDS (Central default 24h) and are refreshed on every session start;
            // the Central token is short-lived, so they can't be re-fetched later from signaling callbacks.
            var result = await _centralServerService.GetIceServersAsync(centralToken);
            _iceServers = IceServerMapper.ToRtcIceServers(result, _fallbackIceServers);
            if (result != null)
            {
                _logger.LogInformation(
                    "ICE servers loaded from Central Server: {Stun} STUN, {Turn} TURN URL(s), TTL={Ttl}s",
                    _iceServers.Count(s => s.urls.StartsWith("stun:", StringComparison.OrdinalIgnoreCase)),
                    _iceServers.Count(s => s.urls.StartsWith("turn:", StringComparison.OrdinalIgnoreCase)),
                    result.Ttl?.ToString() ?? "none");
            }
            else
            {
                _logger.LogWarning("Could not fetch ICE servers from Central Server — using fallback STUN config.");
            }

            // The ticket outlives the access token, so signaling can re-authenticate when its socket
            // reconnects on its own. An older Central has no ticket: the access token it is.
            var ticket = await _centralServerService.GetSignalingTicketAsync(centralToken, centralSessionId);
            if (ticket == null)
                _logger.LogWarning("No GM signaling ticket from the Central Server — signaling can't re-authenticate after the access token expires.");

            await _signaling.ConnectAsync(gameId.ToString(), centralSessionId, ticket ?? centralToken);
        }

        public async Task StopSessionAsync(Guid gameId)
        {
            var game = gameId.ToString();
            await _signaling.DisconnectAsync(game);
            // Players' data channels run peer to peer and outlive signaling, so close them too:
            // their clients see the channel close and wait for the game to come back.
            foreach (var socketId in _peerMeta.Where(kv => kv.Value.GameId == game).Select(kv => kv.Key).ToList())
            {
                CleanupPeer(socketId);
                await _lobbyConnections.ReleaseAsync(socketId);
            }
        }

        // Long enough for the "banned" message to leave before the connection closes.
        private static readonly TimeSpan BannedMessageGrace = TimeSpan.FromMilliseconds(500);

        public async Task DisconnectUserAsync(string centralUserId)
        {
            if (string.IsNullOrEmpty(centralUserId))
                return;
            var sockets = _lobbyConnections.SocketsOf(centralUserId);
            if (sockets.Count == 0)
                return;

            _logger.LogInformation("Disconnecting banned user {UserId} ({Count} connection(s))", centralUserId, sockets.Count);
            foreach (var (_, connection) in sockets)
            {
                try
                {
                    await connection.SendMessageToPlayer(new { command = WebSocketCommandNames.ErrorGeneric, data = BannedError });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not tell banned user {UserId} before disconnecting", centralUserId);
                }
            }
            await Task.Delay(BannedMessageGrace);
            foreach (var (socketId, connection) in sockets)
            {
                CleanupPeer(socketId);
                await _lobbyConnections.ReleaseAsync(socketId, connection);
            }
        }

        private TaskCompletionSource<RTCPeerConnection?> GetOrCreateReadySignal(string socketId)
            => _peerReadySignals.GetOrAdd(socketId, _ =>
                new TaskCompletionSource<RTCPeerConnection?>(TaskCreationOptions.RunContinuationsAsynchronously));

        private Task OnPeerJoined(PeerJoinedArgs args)
        {
            EnsurePeer(args);
            return Task.CompletedTask;
        }

        // Peer-joined and the player's offer can both set up its peer connection (the offer when
        // this backend never saw the peer join), concurrently: the first one does, the other
        // gets that connection.
        private readonly object _peerCreateLock = new();

        private RTCPeerConnection? EnsurePeer(PeerJoinedArgs args)
        {
            lock (_peerCreateLock)
            {
                return _peerConnections.TryGetValue(args.SocketId, out var existing) ? existing : CreatePeer(args);
            }
        }

        private RTCPeerConnection? CreatePeer(PeerJoinedArgs args)
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
                return null;
            }

            _logger.LogInformation("Peer connection created for socketId={SocketId} user={User} game={GameId}",
                args.SocketId, args.Username, args.GameId);

            _peerConnections[args.SocketId] = pc;
            _peerMeta[args.SocketId] = (args.GameId, args.UserId);
            // Peer-joined also comes for a player whose signaling socket reconnected while its
            // data channel stayed open: no offer follows, so don't keep that connection around.
            _ = CloseIfNeverOfferedAsync(args.SocketId, pc);
            _pendingLocalCandidates[args.SocketId] = new List<(string, string?, int?)>();
            GetOrCreateReadySignal(args.SocketId).TrySetResult(pc);

            // This peer connection's lobby connection, once its data channel opened.
            IPlayerConnection? lobbyConnection = null;

            // 'disconnected' gets a grace period to recover; the player client reconnects by
            // itself, so nothing is sent to the player (its other tabs would show the error).
            // Only this connection is let go: the player may have re-offered on the same socket.
            var stateWatcher = new PeerStateWatcher(PeerStateWatcher.DefaultGrace, () =>
            {
                _logger.LogInformation("WebRTC peer {SocketId} in game {GameId} is gone — releasing it", args.SocketId, args.GameId);
                CleanupPeer(args.SocketId, pc);
                if (lobbyConnection != null)
                    _ = _lobbyConnections.ReleaseAsync(args.SocketId, lobbyConnection);
            });
            pc.onconnectionstatechange += state =>
            {
                _logger.LogInformation("WebRTC peer connection state → {State} for socket {SocketId} in game {GameId}", state, args.SocketId, args.GameId);
                stateWatcher.OnStateChanged(state);
            };

            pc.ondatachannel += dataChannel =>
            {
                // Shared state — set by HandleOpenAsync, read by onmessage/onclose.
                WebRTCPlayerConnection? connection = null;
                PlayerDTO? playerEntry = null;
                // chunkId → string slots; lives for the lifetime of this data channel.
                var chunkBuffers = new Dictionary<string, string?[]>();
                // What arrives before HandleOpenAsync knows the player: the client sends its
                // queued requests the moment the channel opens.
                var earlyMessages = new EarlyMessageBuffer();

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
                var banned = false;
                async Task HandleOpenAsync()
                {
                    if (Interlocked.Exchange(ref openHandled, 1) != 0) return;
                    try
                    {
                        if (await ResolvePlayerAsync())
                            earlyMessages.Open(HandleMessage);
                        else if (banned)
                            FailEarlyMessages(403, "You are banned from this server.");
                        else
                            FailEarlyMessages(503, "The game could not identify this player.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Resolving the player for socket {SocketId} in game {GameId} failed", args.SocketId, args.GameId);
                        FailEarlyMessages(503, "The game could not identify this player.");
                    }
                }

                // The requests that came in before the player was known get an answer, not silence.
                void FailEarlyMessages(int status, string reason)
                {
                    foreach (var data in earlyMessages.Fail())
                    {
                        if (TryParseApiRequest(Encoding.UTF8.GetString(data), out var request))
                            SendApiError(dataChannel, request!.Id, status, reason);
                    }
                }


                // Sets connection and playerEntry; false when the player can't be resolved.
                async Task<bool> ResolvePlayerAsync()
                {
                    if (!Guid.TryParse(args.GameId, out var gameId))
                    {
                        SendChannelError(dataChannel, "err: invalid game id");
                        return false;
                    }
                    if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                    {
                        SendChannelError(dataChannel, "err: no lobby found");
                        return false;
                    }

                    // The player client never logs in here, so this is where a ban applies to it.
                    using (var banScope = _serviceScopeFactory.CreateScope())
                    {
                        var banMediator = banScope.ServiceProvider.GetRequiredService<IMediator>();
                        if (!string.IsNullOrEmpty(args.UserId)
                            && await banMediator.Send(new CheckUserBannedCommand { CentralUserId = args.UserId }))
                        {
                            _logger.LogInformation("Refused banned user {UserId} in game {GameId}", args.UserId, gameId);
                            banned = true;
                            SendChannelError(dataChannel, BannedError);
                            return false;
                        }
                    }

                    playerEntry = lobby.ConnectedPlayers.Keys
                        .FirstOrDefault(p => p.CentralServerUserId == args.UserId);

                    if (playerEntry == null)
                    {
                        // Player not yet in lobby (WebRTC opened before or without WebSocket).
                        // Resolve from DB, auto-creating if not found (mirrors AddPlayer flow).
                        using var scope = _serviceScopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        // Username too: a player created below is named after it.
                        var user = new User { Id = args.UserId, UserName = args.Username };

                        var response = await mediator.Send(new GetPlayerCommand
                        {
                            GameID = gameId,
                            User = user
                        });

                        if (response.Player == null)
                        {
                            // Auto-create the player — same path as joining a game for the first time.
                            // The password gate is skipped here: Central signaling only lets the
                            // session's owner and members (who passed its join check, password
                            // included) connect as players, and this path can't prompt for one anyway.
                            await mediator.Send(new AddPlayerCommand { GameID = gameId, User = user, SkipPasswordCheck = true });
                            response = await mediator.Send(new GetPlayerCommand { GameID = gameId, User = user });
                        }

                        if (response.Player == null)
                        {
                            _logger.LogWarning(
                                "WebRTC data channel opened for userId={UserId} but player could not be created in game {GameId}",
                                args.UserId, gameId);
                            SendChannelError(dataChannel, "err: no player found");
                            return false;
                        }

                        connection = new WebRTCPlayerConnection(dataChannel);
                        playerEntry = await LobbyConnectionHelper.ConnectPlayerAsync(lobby, response.Player, connection);
                    }
                    else
                    {
                        connection = new WebRTCPlayerConnection(dataChannel);
                        lobby.ConnectedPlayers[playerEntry].Add(connection);
                    }

                    _lobbyConnections.Track(args.SocketId, args.GameId, args.UserId, connection);
                    lobbyConnection = connection;

                    var elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        - (_peerConnectStartMs.TryGetValue(args.SocketId, out var t) ? t : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    _logger.LogInformation(
                        "Data channel open player={Name} game={GameId} socketId={SocketId} elapsed={Elapsed}ms",
                        playerEntry.Name, gameId, args.SocketId, elapsed);
                    return true;
                }

                dataChannel.onopen += () => _ = HandleOpenAsync();

                // SIPSorcery fires onopen?.Invoke() exactly once at transition time.
                // If the channel was already open before we assigned the handler (race between
                // ondatachannel dispatch and the open transition), call it manually now.
                if (dataChannel.readyState == RTCDataChannelState.open)
                    _ = HandleOpenAsync();

                dataChannel.onclose += () =>
                {
                    if (connection != null)
                        _ = _lobbyConnections.ReleaseAsync(args.SocketId, connection);
                };

                dataChannel.onmessage += (_, _, data) =>
                {
                    switch (earlyMessages.Receive(data))
                    {
                        case EarlyMessageBuffer.Route.Handle:
                            HandleMessage(data);
                            break;
                        case EarlyMessageBuffer.Route.Dropped:
                            // The player couldn't be resolved, or far too much came before it was.
                            var dropped = Encoding.UTF8.GetString(data);
                            _logger.LogWarning(
                                "DROPPED message from socket {SocketId}: its player is unknown ({Length} chars): {Head}",
                                args.SocketId, dropped.Length, dropped.Length > 160 ? dropped[..160] : dropped);
                            break;
                    }
                };

                // A message from the resolved player (connection and playerEntry are set).
                void HandleMessage(byte[] data)
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
                        if (WebRTCHeartbeat.ReplyFor(apiRequest!) is { } heartbeatReply)
                        {
                            if (dataChannel.readyState == RTCDataChannelState.open)
                                dataChannel.send(heartbeatReply);
                            return;
                        }
                        _ = _apiDispatcher.DispatchAsync(apiRequest!, playerEntry, gameId, connection);
                        return;
                    }

                    // Otherwise route as a real-time game command
                    if (_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                        _ = HandleCommandInOrderAsync(lobby, playerEntry, text);
                }
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

            return pc;
        }

        private async Task OnOfferReceived(WebRTCSignalArgs args)
        {
            _logger.LogDebug("Processing WebRTC offer from socketId={SocketId} game={GameId}", args.FromSocketId, args.GameId);

            RTCPeerConnection? pc;
            if (args.UserId != null)
            {
                // Central says who sent it, so the peer connection can be set up here when
                // peer-joined never came (the player joined before this backend reached signaling).
                // A connection that already negotiated is the player's previous one: it re-offers
                // on the same signaling socket after its connection dropped, so start over.
                if (_peerConnections.TryGetValue(args.FromSocketId, out var previous) && previous.remoteDescription != null)
                {
                    _logger.LogInformation("New offer from socket {SocketId}: replacing its previous peer connection", args.FromSocketId);
                    CleanupPeer(args.FromSocketId);
                    await _lobbyConnections.ReleaseAsync(args.FromSocketId);
                }
                pc = EnsurePeer(new PeerJoinedArgs(args.GameId, args.UserId, args.Username ?? args.UserId, args.FromSocketId));
            }
            else
            {
                // An older Central relays no sender: wait for the peer connection peer-joined
                // is creating in parallel (SocketIOClient dispatches both on the thread pool).
                pc = await WaitForPeerConnectionAsync(args.FromSocketId);
            }

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
            // The player's signaling socket left (it may just be reconnecting). Its data channel
            // runs peer to peer, so an established connection stays; its own state ends it.
            if (_peerConnections.TryGetValue(args.SocketId, out var pc)
                && !PeerStateWatcher.ShouldCloseOnSignalingLeave(pc.connectionState))
            {
                _logger.LogInformation("Signaling socket {SocketId} left; keeping its {State} peer connection", args.SocketId, pc.connectionState);
                return Task.CompletedTask;
            }
            CleanupPeer(args.SocketId);
            return _lobbyConnections.ReleaseAsync(args.SocketId);
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

        private static readonly TimeSpan UnofferedPeerTimeout = TimeSpan.FromSeconds(60);

        private async Task CloseIfNeverOfferedAsync(string socketId, RTCPeerConnection pc)
        {
            await Task.Delay(UnofferedPeerTimeout);
            if (pc.remoteDescription != null)
                return;
            _logger.LogInformation("No offer from socket {SocketId} within {Timeout}; closing its peer connection", socketId, UnofferedPeerTimeout);
            CleanupPeer(socketId, pc);
        }

        // `only`: clean up only if that is still the socket's peer connection (not one that
        // replaced it); otherwise just close it.
        private void CleanupPeer(string socketId, RTCPeerConnection? only = null)
        {
            if (only != null && !(_peerConnections.TryGetValue(socketId, out var current) && ReferenceEquals(current, only)))
            {
                only.close();
                return;
            }
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

        // Removes exactly this connection (see PeerLobbyConnections), not the user's first one.
        private Task RemoveDataChannelConnectionAsync(string gameIdStr, string userId, IPlayerConnection connection)
        {
            if (!Guid.TryParse(gameIdStr, out var gameId))
                return Task.CompletedTask;
            if (!_lobbyRegistry.Games.TryGetValue(gameId, out var lobby))
                return Task.CompletedTask;

            var playerEntry = lobby.ConnectedPlayers.Keys
                .FirstOrDefault(p => p.CentralServerUserId == userId);

            return playerEntry == null
                ? Task.CompletedTask
                : LobbyConnectionHelper.DisconnectPlayerAsync(lobby, playerEntry, connection);
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

            return servers;
        }

        // The channel error a banned user gets; the client stops reconnecting on it.
        private const string BannedError = "err: banned";

        private static void SendChannelError(RTCDataChannel dc, string message)
        {
            if (dc.readyState == RTCDataChannelState.open)
                dc.send(JsonConvert.SerializeObject(
                    new { command = WebSocketCommandNames.ErrorGeneric, data = message }));
        }

        // An api-response the client's WebRTCWebHelper resolves its pending request with.
        private static void SendApiError(RTCDataChannel dc, string requestId, int status, string message)
        {
            if (dc.readyState == RTCDataChannelState.open)
                dc.send(JsonConvert.SerializeObject(
                    new { type = "api-response", id = requestId, status, body = new { error = message } }));
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

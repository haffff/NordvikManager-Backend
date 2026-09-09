using DndOnePlaceManager.Application.DataTransferObjects.Game;
using DNDOnePlaceManager.Services.Implementations;
using DNDOnePlaceManager.Services.Interfaces;
using DNDOnePlaceManager.WebRTC;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Spike: does the REAL SignalingService (SocketIOClient-based) work under
    /// browser-wasm against the real local Central Server? Doesn't go through the
    /// shared DI container/harness wiring at all — deliberately standalone so it can't
    /// break WebRTCSessionServiceHarness's TestSignalingService registration.
    /// Progressively more real: a raw transport-only probe with fake credentials, then
    /// the real SignalingService class with fake credentials (transport-level only),
    /// then a full real login + session + authenticate flow with real credentials.
    /// </summary>
    public static class CentralSignalingSpike
    {
        // Set by RunRealLoginSignalingProbeAsync; stays false if any guard bails early.
        // The real-login probe is the one that actually proves something worth recording.
        private static bool _realLoginOk;

        public static async Task RunAsync(IServiceProvider provider)
        {
            // Run the interesting tests first — the default (polling-first) path below
            // is already confirmed broken (browser Fetch API rejects the GET-with-body
            // engine.io polling handshake) and now hangs unpredictably instead of
            // failing fast, so it must not be allowed to block these probes.
            await RunWebSocketOnlyProbeAsync();
            await RunFixedSignalingServiceProbeAsync();
            await RunDefaultSignalingServiceProbeAsync();
            await RunRealLoginSignalingProbeAsync(provider);

            HarnessReport.Record("CentralSignalingSpike", _realLoginOk,
                _realLoginOk
                    ? "GM authenticated signaling for a real session against a real Central Server (WebSocket-only transport)"
                    : "GM did not reach authenticated state — needs a Central Server on :3000 with the 'poc' account");
        }

        /// <summary>
        /// The real deal: log in as an actual user, register a real session, then
        /// authenticate the (fixed-transport) SignalingService against it — end to end
        /// with real Central Server credentials, not fake ones. This is what proves the
        /// GM peer can actually register itself for real players to find, not just that
        /// the socket connects.
        /// </summary>
        private static async Task RunRealLoginSignalingProbeAsync(IServiceProvider provider)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["CentralServerUrl"] = "http://localhost:3000",
                })
                .Build();

            var httpClientFactory = provider.GetRequiredService<System.Net.Http.IHttpClientFactory>();
            var central = new CentralServerService(httpClientFactory, configuration);

            Console.WriteLine("[CentralSignalingSpike] Logging in as 'poc' against the real Central Server...");
            var login = await central.LoginAsync("poc", "pocpocpoc");
            if (login == null)
            {
                Console.WriteLine("[CentralSignalingSpike] RESULT (real login): LoginAsync returned null — check credentials/server.");
                return;
            }
            Console.WriteLine($"[CentralSignalingSpike] Logged in as userId={login.UserId} userName={login.UserName}.");

            var gameId = Guid.NewGuid();
            Console.WriteLine("[CentralSignalingSpike] Creating a real session on the Central Server...");
            var centralSessionId = await central.CreateSessionAsync(login.CentralToken, new GameItemDTO
            {
                Id = gameId,
                Name = "wasm-signaling-spike",
                IsPublic = false,
                PasswordRequired = false,
            });

            if (string.IsNullOrEmpty(centralSessionId))
            {
                Console.WriteLine("[CentralSignalingSpike] RESULT (real login): CreateSessionAsync returned null/empty — check server logs.");
                return;
            }
            Console.WriteLine($"[CentralSignalingSpike] Session created: {centralSessionId}");

            var signalingConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["CentralServerUrl"] = "http://localhost:3000",
                    ["Signaling:ForceWebSocketTransport"] = "true",
                })
                .Build();
            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SimpleWasmLoggerProvider()));
            var signaling = new SignalingService(signalingConfig, loggerFactory.CreateLogger<SignalingService>());

            var authenticated = false;
            var authTcs = new TaskCompletionSource();
            // "authenticated" isn't surfaced as a public event on ISignalingService — but
            // IsConnected() checks internal authenticated-tracking, so poll it instead of
            // needing a new event just for this spike.
            Console.WriteLine("[CentralSignalingSpike] Connecting signaling as GM for the real session...");
            try
            {
                await signaling.ConnectAsync(gameId.ToString(), centralSessionId, login.CentralToken);
                authenticated = signaling.IsConnected(gameId.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CentralSignalingSpike] RESULT (real login): ConnectAsync threw: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine(authenticated
                ? "[CentralSignalingSpike] RESULT (real login): PASS — GM signaling authenticated for a real session against the real Central Server, under wasm."
                : "[CentralSignalingSpike] RESULT (real login): FAIL — connected but never reached authenticated state (see logs above for auth-error/timeout).");

            _realLoginOk = authenticated;

            try { await signaling.DisconnectAsync(gameId.ToString()); } catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// Validates the actual fix (SignalingService.cs's new
        /// "Signaling:ForceWebSocketTransport" config flag) through the real production
        /// class, not just the raw bypass RunWebSocketOnlyProbeAsync uses.
        /// </summary>
        private static async Task RunFixedSignalingServiceProbeAsync()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["CentralServerUrl"] = "http://localhost:3000",
                    ["Signaling:ForceWebSocketTransport"] = "true",
                })
                .Build();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SimpleWasmLoggerProvider()));
            var logger = loggerFactory.CreateLogger<SignalingService>();
            var signaling = new SignalingService(configuration, logger);

            var fakeGameId = Guid.NewGuid().ToString();
            Console.WriteLine("[CentralSignalingSpike] Calling the real SignalingService.ConnectAsync WITH Signaling:ForceWebSocketTransport=true...");
            try
            {
                // ConnectAsync itself waits up to 15s internally for an "authenticated" ack
                // (which will never come with fake credentials) before giving up gracefully —
                // give it 20s so that internal wait has room to finish rather than racing it.
                var connectTask = signaling.ConnectAsync(fakeGameId, "fake-session", "fake-token");
                var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(20)));
                Console.WriteLine(completed == connectTask
                    ? "[CentralSignalingSpike] RESULT (fixed transport, real SignalingService): ConnectAsync returned — the fix works through the real class, not just a raw bypass. (Auth itself is expected to fail/time out internally with fake credentials — that's logged separately and doesn't affect this result.)"
                    : "[CentralSignalingSpike] RESULT (fixed transport, real SignalingService): still hanging after 20s — fix did NOT take effect as expected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CentralSignalingSpike] RESULT (fixed transport, real SignalingService): threw: {ex.GetType().Name}: {ex.Message}");
            }

            try { await signaling.DisconnectAsync(fakeGameId); } catch { /* best-effort cleanup */ }
        }

        private static async Task RunDefaultSignalingServiceProbeAsync()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                {
                    ["CentralServerUrl"] = "http://localhost:3000",
                })
                .Build();

            var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new SimpleWasmLoggerProvider()));
            var logger = loggerFactory.CreateLogger<SignalingService>();

            var signaling = new SignalingService(configuration, logger);

            var fakeGameId = Guid.NewGuid().ToString();
            Console.WriteLine("[CentralSignalingSpike] Calling the real SignalingService.ConnectAsync (default/polling-first transport, already known broken) — capped at 10s so it can't hang the run...");
            try
            {
                var connectTask = signaling.ConnectAsync(fakeGameId, "fake-session", "fake-token");
                var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(10)));
                if (completed != connectTask)
                {
                    Console.WriteLine("[CentralSignalingSpike] RESULT (default transport): timed out after 10s instead of throwing — confirms the polling handshake path is unusable under wasm as-is.");
                }
                else
                {
                    await connectTask; // observe/log any exception
                    Console.WriteLine("[CentralSignalingSpike] RESULT (default transport): ConnectAsync returned without throwing (unexpected given the earlier run's error — re-verify).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CentralSignalingSpike] RESULT (default transport): threw as expected: {ex.GetType().Name}: {ex.Message}");
            }

            try { await signaling.DisconnectAsync(fakeGameId); } catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// SignalingService's default SocketIOOptions leaves transport unset, so
        /// SocketIOClient starts with an HTTP-polling handshake before upgrading to
        /// WebSocket — and that polling handshake is what threw
        /// "GET/HEAD method cannot have body" above (a browser Fetch API restriction
        /// SocketIOClient's HTTP transport doesn't account for). This probes whether
        /// forcing WebSocket-only transport (skipping the polling handshake entirely)
        /// avoids that code path, without touching the shared SignalingService.cs.
        /// </summary>
        private static async Task RunWebSocketOnlyProbeAsync()
        {
            Console.WriteLine("[CentralSignalingSpike] Probing WebSocket-only transport (skips the HTTP-polling handshake that just failed)...");
            try
            {
                var client = new SocketIOClient.SocketIO(new Uri("http://localhost:3000"), new SocketIOClient.SocketIOOptions
                {
                    Transport = SocketIOClient.Common.TransportProtocol.WebSocket,
                });
                client.OnConnected += (sender, e) => Console.WriteLine("[CentralSignalingSpike] WebSocket-only probe: OnConnected fired.");
                client.OnError += (sender, e) => Console.WriteLine($"[CentralSignalingSpike] WebSocket-only probe: OnError: {e}");

                await client.ConnectAsync();
                Console.WriteLine("[CentralSignalingSpike] WebSocket-only probe: ConnectAsync returned without throwing.");
                await Task.Delay(TimeSpan.FromSeconds(2));
                await client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CentralSignalingSpike] WebSocket-only probe threw: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}

using DndOnePlaceManager.Application.Commands.BattleMap;
using DNDOnePlaceManager.Domain.Entities.Auth;
using DNDOnePlaceManager.WebRTC;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NordvikManager.Wasm.SipWrapper;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Integration proof: drives the REAL WebRTCSessionService (not a hand-replicated
    /// stand-in — compare SessionServiceHarness.cs) through a full
    /// peer-joined -> offer -> ICE -> data-channel-open -> chat-message sequence,
    /// against a real GameLobby (via SessionGlue), asserting the message comes back out
    /// over the (fake) data channel. This is the proof that the actual production
    /// wiring — WebRTC transport all the way through to GameLobby.HandleCommand —
    /// works under wasm, not just each half separately.
    /// </summary>
    public static class WebRTCSessionServiceHarness
    {
        private static readonly TaskCompletionSource<string> _messageObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        // api-response messages are correlated by request id (see OnMessageObserved) rather
        // than reusing _messageObserved, since REST-tunnel probes run after the chat-message
        // assertion and need their own matching, not "the next message of any kind".
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> _apiResponses = new();

        public static async Task<bool> RunAsync(IServiceProvider provider)
        {
            // This harness runs before SessionServiceHarness (which also imports this
            // module and initializes the wrapper), so it can't assume either has
            // already happened. Both are cached/idempotent, so calling them again later
            // from SessionServiceHarness is a harmless no-op.
            await WebRtc.InitializeAsync();
            await JSHost.ImportAsync("test-signaling", "/testSignaling.js");

            // Same identity for the game-creating user and the joining WebRTC peer below —
            // mirrors production, where the GM's own session connects as the owner of the
            // game they started. Needed for the REST write-route probes further down, several
            // of which go through Edit-permission checks a non-owner joining player wouldn't pass.
            var userId = Guid.NewGuid().ToString();

            // A real GameModel row must exist — WebRTCSessionService's data-channel-open
            // handler resolves/auto-creates the joining player via GetPlayerCommand /
            // AddPlayerCommand, both of which need a real game to attach the player to
            // (GameLobbyHarness didn't hit this: it added its fake player to
            // GameLobby.ConnectedPlayers directly, bypassing this lookup entirely).
            using (var scope = provider.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var gmUser = new User { Id = userId };
                var createdGameId = await mediator.Send(new AddGameCommand
                {
                    User = gmUser,
                    Name = "wasm-session-harness-game",
                    PasswordRequired = false,
                    IsPublic = false,
                });

                if (createdGameId == null)
                    throw new InvalidOperationException("AddGameCommand returned null — could not create a test game.");

                TestSignalingService.GameId = createdGameId.Value.ToString();
                SessionGlue.StartSession(provider, createdGameId.Value);
            }

            var gameId = Guid.Parse(TestSignalingService.GameId);

            var sessionService = provider.GetRequiredService<IWebRTCSessionService>();
            var signaling = (TestSignalingService)provider.GetRequiredService<ISignalingService>();

            Console.WriteLine("[WebRTCSessionServiceHarness] Starting session (CentralServerService.GetMetaAsync will likely fail to reach a real Central Server — that's expected, WebRTCSessionService falls back to default STUN)...");
            await sessionService.StartSessionAsync(gameId, "test-session", "test-token");

            await signaling.RaisePeerJoinedForTest(userId, "wasm-test-player");

            var offerSdp = await TestSignalingService.CreatePeerAOffer();
            await signaling.RaiseOfferReceivedForTest(offerSdp);

            var completed = await Task.WhenAny(_messageObserved.Task, Task.Delay(TimeSpan.FromSeconds(15)));

            bool pass;
            if (completed != _messageObserved.Task)
            {
                Console.WriteLine("[WebRTCSessionServiceHarness] RESULT: FAIL — no message observed within 15s.");
                pass = false;
            }
            else
            {
                var text = _messageObserved.Task.Result;
                pass = text.Contains("hello from real WebRTCSessionService");
                Console.WriteLine(pass
                    ? "[WebRTCSessionServiceHarness] RESULT: PASS — chat message went through the real WebRTCSessionService -> GameLobby -> ChatHandler -> MediatR pipeline and came back over the data channel."
                    : $"[WebRTCSessionServiceHarness] RESULT: FAIL — unexpected message: {text}");
            }

            if (pass)
                pass = await RunRestDispatcherProbesAsync(gameId);

            SessionGlue.StopSession(provider, gameId);

            HarnessReport.Record("WebRTCSessionServiceHarness", pass,
                pass
                    ? "real WebRTCSessionService -> GameLobby + REST-tunnel dispatcher (read, write routes) round-tripped over a real data channel"
                    : "a stage failed — see [WebRTCSessionServiceHarness] console lines for which");

            return pass;
        }

        /// <summary>
        /// Proves the REST-tunnel dispatcher (WebRTCRestDispatcher, replacing
        /// NotImplementedApiDispatcher) end to end over the same real data channel already
        /// proven above: a mapped route returns 200 with a real body, and an unmapped route
        /// falls back to 501 rather than hanging or throwing.
        /// </summary>
        private static async Task<bool> RunRestDispatcherProbesAsync(Guid gameId)
        {
            var mapped = await SendApiRequestAsync("GET", "api/battlemap/getfullgame");
            if (mapped == null)
            {
                Console.WriteLine("[WebRTCSessionServiceHarness] RESULT: FAIL — no api-response observed for a mapped REST route within 15s.");
                return false;
            }
            var mappedStatus = mapped["status"]?.Value<int>();
            if (mappedStatus != 200 || mapped["body"] == null || mapped["body"]!.Type == JTokenType.Null)
            {
                Console.WriteLine($"[WebRTCSessionServiceHarness] RESULT: FAIL — mapped REST route returned status={mappedStatus}, body={mapped["body"]}.");
                return false;
            }

            var unmapped = await SendApiRequestAsync("GET", "api/nonexistent/route");
            if (unmapped == null)
            {
                Console.WriteLine("[WebRTCSessionServiceHarness] RESULT: FAIL — no api-response observed for an unmapped REST route within 15s.");
                return false;
            }
            var unmappedStatus = unmapped["status"]?.Value<int>();
            if (unmappedStatus != 501)
            {
                Console.WriteLine($"[WebRTCSessionServiceHarness] RESULT: FAIL — unmapped REST route expected status=501, got {unmappedStatus}.");
                return false;
            }

            Console.WriteLine("[WebRTCSessionServiceHarness] RESULT: PASS — REST-tunnel dispatcher returned 200 with a real body for a mapped route and 501 for an unmapped route, over the real data channel.");

            return await RunWriteRouteProbesAsync(gameId);
        }

        /// <summary>
        /// Proves write routes round-trip through the dispatcher, MediatR, and (for the
        /// two that have one) the lobby broadcast machinery, without throwing. Addon-domain
        /// routes are skipped here — the test player isn't the game's owner, so those would
        /// hit the manual "not owner &amp; no Permission.Edit -> 403" check rather than
        /// exercising the not-found path, which needs deliberate permission setup the plan
        /// marked as optional/lower-priority for this pass.
        /// </summary>
        private static async Task<bool> RunWriteRouteProbesAsync(Guid gameId)
        {
            // ParentID must reference a real entity — AddPropertiesCommandHandler looks it up
            // via DetectEntityTypeAsync/Find. The test game itself (GameModel) already exists
            // and is a supported parent type, so use it rather than a fabricated Guid.
            var addBulkBody = new JArray
            {
                new JObject { ["name"] = "wasm-rest-write-probe", ["value"] = "hello", ["entityName"] = "wasm-rest-write-probe", ["parentId"] = gameId }
            };
            var addBulk = await SendApiRequestAsync("POST", "api/properties/addbulk", addBulkBody);
            if (addBulk?["status"]?.Value<int>() != 200)
            {
                Console.WriteLine($"[WebRTCSessionServiceHarness] RESULT: FAIL — properties/addbulk expected status=200, got {addBulk?["status"]}.");
                return false;
            }

            var createResourceBody = new JObject
            {
                ["key"] = "wasm-rest-write-probe",
                ["name"] = "wasm-rest-write-probe",
                ["content"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("probe")),
                ["mimeType"] = "text/plain",
            };
            var createResource = await SendApiRequestAsync("POST", "api/materials/createresource", createResourceBody);
            if (createResource?["status"]?.Value<int>() != 200 || createResource["body"]?["id"] == null)
            {
                Console.WriteLine($"[WebRTCSessionServiceHarness] RESULT: FAIL — materials/createresource expected status=200 with an id, got {createResource}.");
                return false;
            }

            var playSoundBody = new JObject { ["resourceId"] = createResource["body"]!["id"]!.ToString() };
            var playSound = await SendApiRequestAsync("POST", "api/soundboard/playsound", playSoundBody);
            if (playSound?["status"]?.Value<int>() != 200)
            {
                Console.WriteLine($"[WebRTCSessionServiceHarness] RESULT: FAIL — soundboard/playsound expected status=200, got {playSound?["status"]}.");
                return false;
            }

            Console.WriteLine("[WebRTCSessionServiceHarness] RESULT: PASS — write routes (properties/addbulk, materials/createresource, soundboard/playsound) round-tripped through the dispatcher over the real data channel.");
            return true;
        }

        private static Task<JObject?> SendApiRequestAsync(string method, string path) =>
            SendApiRequestAsync(method, path, null);

        private static async Task<JObject?> SendApiRequestAsync(string method, string path, JToken? body)
        {
            var id = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _apiResponses[id] = tcs;

            var request = new JObject
            {
                ["type"] = "api-request",
                ["id"] = id,
                ["method"] = method,
                ["path"] = path,
                ["query"] = new JObject(),
                ["body"] = body ?? JValue.CreateNull()
            };
            await TestSignalingService.SendFromPeerA(request.ToString(Newtonsoft.Json.Formatting.None));

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            _apiResponses.TryRemove(id, out _);
            return completed == tcs.Task ? tcs.Task.Result : null;
        }

        // GameLobby broadcasts other things first — e.g. a "player_join" notification
        // fires the moment LobbyConnectionHelper.ConnectPlayerAsync runs, before our
        // chat message is even sent. Only resolve on the message we're actually
        // looking for; ignore everything else instead of treating the first message
        // as proof (that would only prove player-join broadcast, not the chat/MediatR path).
        internal static void OnMessageObserved(string text)
        {
            if (text.Length > 0 && text[0] == '{')
            {
                JObject? obj = null;
                try { obj = JObject.Parse(text); } catch { /* not JSON, fall through */ }

                if (obj != null && obj["type"]?.Value<string>() == "api-response")
                {
                    var id = obj["id"]?.Value<string>();
                    if (id != null && _apiResponses.TryGetValue(id, out var tcs))
                        tcs.TrySetResult(obj);
                    return;
                }
            }

            if (text.Contains("hello from real WebRTCSessionService"))
                _messageObserved.TrySetResult(text);
        }
    }
}

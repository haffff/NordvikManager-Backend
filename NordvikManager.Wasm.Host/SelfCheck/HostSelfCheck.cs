using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NordvikManager.Application.Commands.BattleMap;
using NordvikManager.Application.Commands.Game.Player.GetPlayer;
using NordvikManager.Domain.Entities.Auth;
using NordvikManager.Services.Implementations;
using NordvikManager.Wasm.Hosting;
using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NordvikManager.Wasm.Host.SelfCheck
{
    /// <summary>
    /// Runs the GM host in this tab as it would host a game, with Central left out: the real
    /// WasmHost composition, WebRTCSessionService and GameLobby, the browser's WebRTC, and an
    /// in-page player (selfcheck-player.js) connected through <see cref="InPageSignaling"/>.
    /// Prints one CHECK line per step.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public static partial class HostSelfCheck
    {
        private static InPageSignaling? _signaling;
        private static string _gameId = "";
        private const string PlayerSocket = "socket-1";
        private static readonly string PlayerUserId = Guid.NewGuid().ToString();

        [JSImport("init", "selfcheck-player")] private static partial Task PlayerInit();
        [JSImport("offer", "selfcheck-player")] private static partial Task<string> PlayerOffer();
        [JSImport("setAnswer", "selfcheck-player")] private static partial Task PlayerSetAnswer(string sdp);
        [JSImport("addCandidate", "selfcheck-player")] private static partial void PlayerAddCandidate(string candidate, string? sdpMid, int index);
        [JSImport("waitOpen", "selfcheck-player")] private static partial Task PlayerWaitOpen();
        [JSImport("request", "selfcheck-player")] private static partial Task<string> PlayerRequest(string method, string path);
        [JSImport("sendCommand", "selfcheck-player")] private static partial void PlayerSendCommand(string json);
        [JSImport("waitForCommand", "selfcheck-player")] private static partial Task<string> PlayerWaitForCommand(string command, string data);

        [JSExport]
        internal static void OnPlayerCandidate(string candidate, string? sdpMid, int index)
            => _ = _signaling!.RaiseCandidateAsync(new IceCandidateArgs(_gameId, PlayerSocket, candidate, sdpMid, index < 0 ? null : index, PlayerUserId));

        public static async Task<bool> RunAsync()
        {
            var passed = true;
            async Task Check(string name, Func<Task<string>> check)
            {
                try
                {
                    Console.WriteLine($"CHECK {name}: PASS {await check()}");
                }
                catch (Exception ex)
                {
                    passed = false;
                    Console.WriteLine($"CHECK {name}: FAIL {ex}");
                    throw;
                }
            }

            try
            {
                await JsBrowserRtc.InitializeAsync();
                await JSHost.ImportAsync("selfcheck-player", "../selfcheck-player.js");
                await PlayerInit();

                _signaling = new InPageSignaling();
                ServiceProvider provider = null!;
                await Check("host builds", () =>
                {
                    // Central is never reached: signaling is in the page, no session is started.
                    provider = WasmHost.Build(WasmHost.Configuration("http://localhost:9"), new JsBrowserRtc(),
                        services => services.Replace(ServiceDescriptor.Singleton<ISignalingService>(_signaling)));
                    return Task.FromResult("every registration validated");
                });

                Guid gameId = default;
                await Check("game created and listed (MediatR, EF Core in memory)", async () =>
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
                    gameId = await mediator.Send(new AddGameCommand { User = gm, Name = "Self-check game", PasswordRequired = false })
                        ?? throw new Exception("AddGameCommand gave no game");
                    var list = await mediator.Send(new GetGameListCommand { UserId = gm.Id });
                    if (list.GameItemList?.Any(g => g.Id == gameId) != true)
                        throw new Exception("the game isn't listed");
                    return $"game {gameId}";
                });
                _gameId = gameId.ToString();

                await Check("lobby opened", async () =>
                {
                    // As SessionController.StartSession does, before any peer connects.
                    using var scope = provider.CreateScope();
                    var systemPlayer = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new GetSystemPlayerCommand { GameID = gameId });
                    provider.GetRequiredService<ILobbyRegistry>().Games.GetOrAdd(gameId, _ => new GameLobby(provider.GetRequiredService<IServiceScopeFactory>())
                    {
                        GameId = gameId,
                        SystemPlayer = systemPlayer,
                    });
                    return "lobby registered";
                });

                await Check("player connects (WebRTCSessionService, browser WebRTC)", async () =>
                {
                    provider.GetRequiredService<IWebRTCSessionService>(); // subscribes to signaling
                    _signaling.AnswerSent = PlayerSetAnswer;
                    _signaling.CandidateSent = (c, mid, index) => PlayerAddCandidate(c, mid, index ?? -1);
                    var offer = await PlayerOffer();
                    await _signaling.RaiseOfferAsync(new WebRTCSignalArgs(_gameId, PlayerSocket, offer, PlayerUserId, "Alice"));
                    await Within(PlayerWaitOpen(), "the data channel to open");
                    return "data channel open";
                });

                await Check("heartbeat answered", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", WebRTCHeartbeat.Path), "the heartbeat reply"));
                    Expect((int?)response["status"] == 200, "status " + response["status"]);
                    return "200";
                });

                await Check("tunnelled REST request answered 501 (no dispatcher yet)", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/battlemap/GetFullGame"), "the api-response"));
                    Expect((int?)response["status"] == 501, "status " + response["status"]);
                    return (string?)response["body"]?["error"] ?? "";
                });

                await Check("chat through GameLobby (resolves the player, ChatHandler, MediatR) comes back", async () =>
                {
                    PlayerSendCommand(JsonConvert.SerializeObject(new { command = "chat_push", data = "hello from the player", gameId }));
                    var echo = JObject.Parse(await Within(PlayerWaitForCommand("chat_push", "hello from the player"), "the chat message back"));
                    return "result " + echo["result"];
                });

                await Check("over 15 000 chars both ways (chunk reassembly, send pump)", async () =>
                {
                    var big = "big:" + string.Concat(Enumerable.Repeat("zażółć gęślą jaźń 🎲 ", 1500));
                    PlayerSendCommand(JsonConvert.SerializeObject(new { command = "chat_push", data = big, gameId }));
                    var echo = JObject.Parse(await Within(PlayerWaitForCommand("chat_push", big), "the big chat message back"));
                    Expect(echo["__chunked"]?.Value<bool>() == true, "the reply wasn't chunked");
                    return $"{big.Length} chars, reply chunked";
                });
            }
            catch (Exception ex)
            {
                passed = false;
                Console.WriteLine("Self-check stopped: " + ex.Message);
            }
            return passed;
        }

        private static void Expect(bool ok, string what)
        {
            if (!ok) throw new Exception("unexpected: " + what);
        }

        private static async Task Within(Task task, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(20))) != task)
                throw new TimeoutException("timed out waiting for " + what);
            await task;
        }

        private static async Task<T> Within<T>(Task<T> task, string what)
        {
            await Within((Task)task, what);
            return task.Result;
        }
    }
}

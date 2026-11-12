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

        /// <summary>The self-check's own file on OPFS (host-worker.js mounts it; the GM's data.db isn't touched).</summary>
        public const string SelfCheckDatabase = "/opfs/selfcheck.db";
        public const string SelfCheckStorage = "selfcheck-storage";

        // How many times opfs-fs.js flushed a file to OPFS (fsync, close, delete), and how many of
        // those were SQLite's fsync.
        private static double Flushes() => JSHost.GlobalThis.GetPropertyAsJSObject("nordvikOpfs")?.GetPropertyAsDouble("flushes") ?? 0;
        private static double Fsyncs() => JSHost.GlobalThis.GetPropertyAsJSObject("nordvikOpfs")?.GetPropertyAsDouble("fsyncs") ?? 0;

        [JSImport("init", "selfcheck-player")] private static partial Task PlayerInit();
        [JSImport("offer", "selfcheck-player")] private static partial Task<string> PlayerOffer();
        [JSImport("setAnswer", "selfcheck-player")] private static partial Task PlayerSetAnswer(string sdp);
        [JSImport("addCandidate", "selfcheck-player")] private static partial void PlayerAddCandidate(string candidate, string? sdpMid, int index);
        [JSImport("waitOpen", "selfcheck-player")] private static partial Task PlayerWaitOpen();
        [JSImport("request", "selfcheck-player")] private static partial Task<string> PlayerRequest(string method, string path, string? bodyJson, string? queryJson);

        private static Task<string> PlayerRequest(string method, string path) => PlayerRequest(method, path, null, null);
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
                await JsBrowserRtc.InitializeAsync(HostPage.WorkerRtcModule);
                // In the page (it's an RTCPeerConnection): reached through the worker bridge.
                await JSHost.ImportAsync("selfcheck-player", "../selfcheck-player-worker.js");
                await PlayerInit();

                _signaling = new InPageSignaling();
                ServiceProvider provider = null!;
                // The self-check's own file storage folder on OPFS, never the GM's storage/.
                var browserFiles = await JsBrowserFiles.CreateAsync();
                foreach (var left in await browserFiles.ListAsync(SelfCheckStorage))
                    await browserFiles.DeleteAsync(left.Path);
                var fileStorage = await BrowserFileStorage.OpenAsync(browserFiles, SelfCheckStorage);
                await Check("host builds", () =>
                {
                    // Central is never reached: signaling is in the page, no session is started.
                    provider = WasmHost.Build(WasmHost.Configuration("http://localhost:9"), new JsBrowserRtc(),
                        services => services.Replace(ServiceDescriptor.Singleton<ISignalingService>(_signaling)), files: fileStorage);
                    return Task.FromResult("every registration validated");
                });

                await Check("runs in a worker (the page keeps the UI and WebRTC)", () =>
                {
                    var global = JSHost.GlobalThis;
                    Expect(global.HasProperty("WorkerGlobalScope") && !global.HasProperty("document"), "not in a worker");
                    return Task.FromResult("dedicated worker, no DOM");
                });

                await Check("games kept in OPFS (a fresh database migrates on it, survives a rebuild)", async () =>
                {
                    var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
                    var name = "Kept " + Guid.NewGuid().ToString("N")[..8];
                    // A fresh file each run (on OPFS, deleting empties it).
                    System.IO.File.Delete(SelfCheckDatabase);
                    var before = Flushes();
                    await using (var first = WasmHost.Build(WasmHost.Configuration("http://localhost:9", databasePath: SelfCheckDatabase), new JsBrowserRtc(),
                        services => services.Replace(ServiceDescriptor.Singleton<ISignalingService>(new InPageSignaling()))))
                    {
                        using var scope = first.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AddGameCommand { User = gm, Name = name, PasswordRequired = false });
                    }
                    var flushed = Flushes() - before;
                    Expect(flushed > 0, "no commit was flushed to OPFS");

                    // As after a reload: a new host on the same file.
                    await using var reloaded = WasmHost.Build(WasmHost.Configuration("http://localhost:9", databasePath: SelfCheckDatabase), new JsBrowserRtc(),
                        services => services.Replace(ServiceDescriptor.Singleton<ISignalingService>(new InPageSignaling())));
                    using var check = reloaded.CreateScope();
                    var list = await check.ServiceProvider.GetRequiredService<IMediator>().Send(new GetGameListCommand { UserId = gm.Id });
                    Expect(list.GameItemList?.Any(g => g.Name == name) == true, "the game isn't there after a rebuild");
                    var bytes = new System.IO.FileInfo(SelfCheckDatabase).Length;

                    // A commit on a connection that stays open: SQLite's own fsync reaches OPFS
                    // (not only the flushes of closing or deleting a file).
                    using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={SelfCheckDatabase};Pooling=False"))
                    {
                        connection.Open();
                        using var setup = connection.CreateCommand();
                        setup.CommandText = "CREATE TABLE IF NOT EXISTS selfcheck_commits (x);";
                        setup.ExecuteNonQuery();
                        var fsyncsBefore = Fsyncs();
                        using var insert = connection.CreateCommand();
                        insert.CommandText = "INSERT INTO selfcheck_commits VALUES (1);";
                        insert.ExecuteNonQuery();
                        Expect(Fsyncs() > fsyncsBefore, "a commit didn't fsync to OPFS");
                    }
                    // Building these hosts pointed the process-wide PermissionsExtension at them: back to the main one.
                    NordvikManager.Application.ApplicationLayerModule.AfterBuild(provider);
                    return $"'{name}' kept, listed by a new host ({bytes} bytes on OPFS, {flushed} flushes); a commit fsyncs to OPFS";
                });

                await Check("files kept in OPFS (big uploads, files cards save as files)", async () =>
                {
                    Expect(provider.GetRequiredService<NordvikManager.Infrastructure.Interfaces.IFileStorageProvider>() == fileStorage, "the host doesn't use BrowserFileStorage");
                    var game = Guid.NewGuid();
                    var random = new Random(7);
                    var first = new byte[200_000];
                    var second = new byte[150_000];
                    random.NextBytes(first);
                    random.NextBytes(second);
                    var resource = Guid.NewGuid();
                    var saved = await fileStorage.SaveAsync(game, resource, first, ".bin");
                    // A card saving the file again: a new file moved over the old one.
                    await fileStorage.SaveAsync(game, resource, second, ".bin");
                    Expect((await fileStorage.ReadAsync(saved))!.AsSpan().SequenceEqual(second), "the saved-again file reads back wrong");

                    // A finished upload (in the runtime's memory) copied in, in parts.
                    var upload = "/tmp/selfcheck-upload.part";
                    System.IO.Directory.CreateDirectory("/tmp");
                    var big = new byte[3 * 1024 * 1024 + 123];
                    random.NextBytes(big);
                    await System.IO.File.WriteAllBytesAsync(upload, big);
                    var moved = await fileStorage.MoveInAsync(game, Guid.NewGuid(), upload, ".ogg");
                    Expect(!System.IO.File.Exists(upload), "the upload's parts file is still there");

                    // Two ranges of one file at once (one handle per file: it locks the file).
                    var ranges = await Task.WhenAll(fileStorage.ReadRangeAsync(moved, 1000, 2_000_000), fileStorage.ReadRangeAsync(moved, 1200, 4000));
                    Expect(ranges[0]!.AsSpan().SequenceEqual(big.AsSpan(1000, 2_000_000)) && ranges[1]!.AsSpan().SequenceEqual(big.AsSpan(1200, 4000)), "overlapping ranges read wrong");

                    // As after a reload: what OPFS lists, with the same stamps (the resource cache's versions).
                    var reopened = await BrowserFileStorage.OpenAsync(browserFiles, SelfCheckStorage);
                    foreach (var path in new[] { saved, moved })
                    {
                        Expect(reopened.Exists(path), $"{path} isn't listed by a new storage");
                        Expect(reopened.GetStamp(path) == fileStorage.GetStamp(path), $"{path}'s stamp changed: {fileStorage.GetStamp(path)} -> {reopened.GetStamp(path)}");
                    }
                    Expect((await reopened.ReadAsync(moved))!.AsSpan().SequenceEqual(big), "the moved-in file reads back wrong");
                    var listed = await browserFiles.ListAsync(SelfCheckStorage);
                    Expect(listed.Count == 2, "OPFS has " + string.Join(", ", listed.Select(f => f.Path)));

                    await fileStorage.DeleteAsync(saved);
                    await fileStorage.DeleteAsync(moved);
                    Expect((await browserFiles.ListAsync(SelfCheckStorage)).Count == 0 && !fileStorage.Exists(moved), "deleted files are still there");
                    return $"saved, saved again, a {big.Length}-byte upload moved in, overlapping ranges, same stamps after reopening, deleted";
                });

                Guid gameId = default;
                var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
                await Check("game created and listed (MediatR, EF Core in memory)", async () =>
                {
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    gameId = await mediator.Send(new AddGameCommand { User = gm, Name = "Self-check game", PasswordRequired = false })
                        ?? throw new Exception("AddGameCommand gave no game");
                    var list = await mediator.Send(new GetGameListCommand { UserId = gm.Id });
                    if (list.GameItemList?.Any(g => g.Id == gameId) != true)
                        throw new Exception("the game isn't listed");
                    return $"game {gameId}";
                });
                _gameId = gameId.ToString();

                await Check("the GM uploads a file kept as a file: on OPFS, read back in ranges", async () =>
                {
                    var router = provider.GetRequiredService<NordvikManager.Api.ApiRouter>();
                    var context = new NordvikManager.Api.ApiContext(gm, null);
                    var query = new Dictionary<string, string> { ["gameId"] = gameId.ToString() };
                    var bytes = new byte[3 * 1024 * 1024 + 7];
                    new Random(11).NextBytes(bytes);
                    var (status, body) = await router.InvokeAsync(context, "POST", "api/Materials/UploadStart",
                        JObject.FromObject(new { name = "music.ogg", mimeType = "audio/ogg", size = bytes.Length, storageKind = 1 }), query);
                    Expect(status == 200, $"UploadStart {status} {body}");
                    var uploadId = (string?)body?["uploadId"];
                    const int part = 1024 * 1024;
                    for (var offset = 0; offset < bytes.Length; offset += part)
                    {
                        (status, body) = await router.InvokeAsync(context, "POST", "api/Materials/UploadPart",
                            JObject.FromObject(new { uploadId, offset, data = Convert.ToBase64String(bytes, offset, Math.Min(part, bytes.Length - offset)) }), query);
                        Expect(status == 200, $"UploadPart {status} {body}");
                    }
                    (status, body) = await router.InvokeAsync(context, "POST", "api/Materials/UploadFinish", JObject.FromObject(new { uploadId }), query);
                    Expect(status == 200, $"UploadFinish {status} {body}");
                    var id = (string?)body?["id"];

                    var onOpfs = await browserFiles.ListAsync($"{SelfCheckStorage}/games/{gameId}");
                    Expect(onOpfs.Count == 1 && onOpfs[0].Size == bytes.Length, "on OPFS: " + string.Join(", ", onOpfs.Select(f => $"{f.Path} {f.Size}")));
                    (status, body) = await router.InvokeAsync(context, "GET", "api/Materials/ResourceRange", null,
                        new Dictionary<string, string> { ["gameId"] = gameId.ToString(), ["id"] = id!, ["start"] = "2000000", ["length"] = "65536" });
                    Expect(status == 200 && (long?)body?["total"] == bytes.Length, $"ResourceRange {status} {body?["total"]}");
                    Expect(Convert.FromBase64String((string)body!["data"]!).AsSpan().SequenceEqual(bytes.AsSpan(2_000_000, 65536)), "the range read back differs");
                    return $"{bytes.Length} bytes in {(bytes.Length + part - 1) / part} parts, {onOpfs[0].Path}, a range read back equal (version {body["version"]})";
                });

                await Check("api/Host/Info says browser, with what it doesn't provide", async () =>
                {
                    var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
                    var (status, body) = await provider.GetRequiredService<NordvikManager.Api.ApiRouter>()
                        .InvokeAsync(new NordvikManager.Api.ApiContext(gm, null), "GET", "api/Host/Info");
                    if (status != 200 || (string?)body?["kind"] != "browser")
                        throw new Exception($"{status} {body}");
                    var unavailable = body["unavailable"]?.Count() ?? 0;
                    var limitations = body["limitations"]?.Count() ?? 0;
                    if (unavailable == 0 || limitations == 0)
                        throw new Exception($"empty lists: {body}");
                    return $"version {body["version"]}, {unavailable} unavailable, {limitations} limitations";
                });

                await Check("built-in addons in the bundle (Basics installed in a new game)", async () =>
                {
                    var builtIns = provider.GetRequiredService<NordvikManager.Application.Services.IBuiltInAddons>().GetAll();
                    if (!builtIns.Any(b => b.Key == "basics"))
                        throw new Exception($"no Basics under {AppContext.BaseDirectory} ({builtIns.Count} built-ins)");
                    using var scope = provider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    var gm = new User { Id = Guid.NewGuid().ToString(), UserName = "gm" };
                    var withBasics = await mediator.Send(new AddGameCommand { User = gm, Name = "Basics game", PasswordRequired = false, AddonsSelected = new[] { "basics" } })
                        ?? throw new Exception("AddGameCommand gave no game");
                    var system = await mediator.Send(new GetSystemPlayerCommand { GameID = withBasics });
                    // AddGame installs them in the background (as natively): wait for it.
                    var started = DateTime.UtcNow;
                    while (true)
                    {
                        using var check = provider.CreateScope();
                        var addons = await check.ServiceProvider.GetRequiredService<IMediator>().Send(
                            new NordvikManager.Application.Commands.Addons.GetAddons.GetAddonsCommand { GameId = withBasics, Player = system, Flat = true });
                        if (addons.Any(a => a.Key == "basics"))
                            break;
                        if (DateTime.UtcNow - started > TimeSpan.FromSeconds(30))
                            throw new Exception($"Basics wasn't installed within 30 s ({string.Join(", ", addons.Select(a => a.Key))})");
                        await Task.Delay(250);
                    }
                    return $"{string.Join(", ", builtIns.Select(b => $"{b.Key} {b.Version}"))}; installed in a new game in {(DateTime.UtcNow - started).TotalSeconds:0.0} s";
                });

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

                await Check("tunnelled GetPlayer answered by the shared endpoint (ApiRouter)", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/battlemap/GetPlayer"), "the api-response"));
                    Expect((int?)response["status"] == 200, "status " + response["status"] + " " + response["body"]);
                    Expect((string?)response["body"]?["name"] == "Alice", "player " + response["body"]?["name"]);
                    Expect((string?)response["body"]?["centralServerUserId"] == PlayerUserId, "camelCase body with the caller's id");
                    return "200, player Alice";
                });

                await Check("tunnelled GetFullGame answered by the shared endpoint", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/battlemap/GetFullGame"), "the api-response"));
                    Expect((int?)response["status"] == 200, "status " + response["status"] + " " + response["body"]);
                    Expect((string?)response["body"]?["name"] == "Self-check game", "game " + response["body"]?["name"]);
                    return $"200, game '{response["body"]?["name"]}'";
                });

                await Check("tunnelled session/whoami gives the channel's game (ApiContext)", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/session/whoami"), "the api-response"));
                    Expect((int?)response["status"] == 200, "status " + response["status"]);
                    Expect((string?)response["body"]?["gameId"] == _gameId, "game " + response["body"]?["gameId"]);
                    return "200, the session's game";
                });

                await Check("tunnelled user/userinfo gives the caller", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/user/userinfo"), "the api-response"));
                    Expect((int?)response["status"] == 200, "status " + response["status"] + " " + response["body"]);
                    return "200 " + response["body"]?.ToString(Newtonsoft.Json.Formatting.None);
                });

                await Check("a server-only route answers 501 (api/meta)", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/meta"), "the api-response"));
                    Expect((int?)response["status"] == 501, "status " + response["status"]);
                    return (string?)response["body"]?["error"] ?? "";
                });

                await Check("a file uploaded in parts and read back (Materials, upload store, quota)", async () =>
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("map tile ", 3000)));
                    var start = JObject.Parse(await Within(PlayerRequest("POST", "/api/materials/UploadStart",
                        JsonConvert.SerializeObject(new { name = "map.png", mimeType = "image/png", size = bytes.Length }), null), "UploadStart"));
                    Expect((int?)start["status"] == 200, "UploadStart " + start["status"] + " " + start["body"]);
                    var uploadId = (string?)start["body"]?["uploadId"];
                    var half = bytes.Length / 2;
                    foreach (var (offset, length) in new[] { (0, half), (half, bytes.Length - half) })
                    {
                        var part = JObject.Parse(await Within(PlayerRequest("POST", "/api/materials/UploadPart",
                            JsonConvert.SerializeObject(new { uploadId, offset, data = Convert.ToBase64String(bytes, offset, length) }), null), "UploadPart"));
                        Expect((int?)part["status"] == 200, "UploadPart " + part["status"] + " " + part["body"]);
                    }
                    var finish = JObject.Parse(await Within(PlayerRequest("POST", "/api/materials/UploadFinish",
                        JsonConvert.SerializeObject(new { uploadId }), null), "UploadFinish"));
                    Expect((int?)finish["status"] == 200, "UploadFinish " + finish["status"] + " " + finish["body"]);
                    var id = (string?)finish["body"]?["id"];
                    var read = JObject.Parse(await Within(PlayerRequest("GET", "/api/materials/resource", null,
                        JsonConvert.SerializeObject(new { id })), "the resource"));
                    Expect((int?)read["status"] == 200, "resource " + read["status"]);
                    Expect((string?)read["body"]?["data"] == Convert.ToBase64String(bytes), "the bytes read back differ");
                    return $"{bytes.Length} bytes in 2 parts, read back equal";
                });

                await Check("addon step definitions (Addon)", async () =>
                {
                    var response = JObject.Parse(await Within(PlayerRequest("GET", "/api/addon/StepDefinitions"), "the api-response"));
                    Expect((int?)response["status"] == 200, "status " + response["status"] + " " + response["body"]);
                    return "200";
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

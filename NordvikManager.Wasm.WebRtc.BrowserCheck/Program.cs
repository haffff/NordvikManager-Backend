// A real-browser check of NordvikManager.Wasm.WebRtc: a plain JS RTCPeerConnection (player.js,
// offering like the player client) connects to a GM peer made by BrowserPeerConnectionFactory
// over JsBrowserRtc + nordvik-webrtc.js, and they exchange messages. Prints RESULT: PASS/FAIL.
using NordvikManager.Wasm.WebRtc;
using NordvikManager.WebRTC;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

try
{
    await Harness.RunAsync();
    Console.WriteLine("RESULT: PASS");
}
catch (Exception ex)
{
    Console.WriteLine("RESULT: FAIL " + ex);
}

public static partial class Harness
{
    private static IPeerConnection? _gm;
    private static readonly TaskCompletionSource ChannelClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [JSImport("init", "player")] private static partial Task PlayerInit();
    [JSImport("offer", "player")] private static partial Task<string> PlayerOffer();
    [JSImport("setAnswer", "player")] private static partial Task PlayerSetAnswer(string sdp);

    [JSExport]
    public static void OnPlayerCandidate(string candidate, string? sdpMid, int index)
    {
        _playerCandidates++;
        _gm!.AddRemoteCandidate(new IceCandidate(candidate, sdpMid, index < 0 ? null : index));
    }

    private static int _playerCandidates;

    [JSExport]
    public static void OnPlayerChannelClosed() => ChannelClosed.TrySetResult();

    public static async Task RunAsync()
    {
        // The shared layers serialize with reflection-based System.Text.Json; the csproj turns it on.
        var json = System.Text.Json.JsonSerializer.Serialize(new { reflection = "on" });
        if (json != "{\"reflection\":\"on\"}") throw new Exception("System.Text.Json reflection gave " + json);
        Console.WriteLine("[gm] reflection-based System.Text.Json works: " + json);

        await JsBrowserRtc.InitializeAsync();
        await JSHost.ImportAsync("player", "../player.js");
        await PlayerInit();

        var factory = new BrowserPeerConnectionFactory(new JsBrowserRtc());
        // No ICE servers: host candidates only (loopback). TURN credentials are still passed
        // through, to check the browser accepts them.
        _gm = factory.Create(new[] { new IceServer("turn:127.0.0.1:3478?transport=udp", "123:user", "secret=") });

        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gotAck = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var localCandidates = 0;
        _gm.StateChanged += s =>
        {
            Console.WriteLine("[gm] state " + s);
            if (s == PeerState.Connected) connected.TrySetResult();
        };
        // Counted, not sent: the player then never learns where the GM is, so only the GM's
        // checks against the player's (queued) candidates can connect them.
        _gm.LocalCandidate += _ => localCandidates++;
        _gm.DataChannel += channel =>
        {
            channel.Message += data => gotAck.TrySetResult(Encoding.UTF8.GetString(data));
            void Greet()
            {
                Console.WriteLine($"[gm] channel open, buffered={channel.BufferedAmount}");
                channel.Send("hello-from-gm");
            }
            channel.Opened += Greet;
            if (channel.IsOpen) Greet();
        };

        // Returns once the player gathered every candidate; they all reached the GM already.
        var offer = await PlayerOffer();
        if (offer.Contains("a=candidate")) throw new Exception("the offer carries candidates; the check needs them separate");
        if (_playerCandidates == 0) throw new Exception("the player gathered no candidates");
        Console.WriteLine($"[gm] {_playerCandidates} player candidate(s) arrived before the offer was set");
        var rejected = await _gm.SetRemoteOfferAsync(offer);
        if (rejected != null) throw new Exception("offer rejected: " + rejected);
        if (!_gm.HasRemoteDescription) throw new Exception("no remote description after the offer");

        var bad = await factory.Create(Array.Empty<IceServer>()).SetRemoteOfferAsync("not sdp");
        Console.WriteLine("[gm] bad offer gives: " + bad);
        if (bad == null) throw new Exception("a bad offer was accepted");

        var answer = await _gm.CreateAnswerAsync();
        if (!answer.Contains("a=setup:")) throw new Exception("answer doesn't look like SDP");
        await PlayerSetAnswer(answer);

        await Within(connected.Task, "connected");
        var ack = await Within(gotAck.Task, "player's reply");
        Console.WriteLine("[gm] got " + ack);
        if (ack != "ack:hello-from-gm:zażółć 🎲") throw new Exception("unexpected reply: " + ack);
        if (localCandidates == 0) throw new Exception("no local candidates were raised");

        _gm.Close();
        if (_gm.State != PeerState.Closed) throw new Exception("not closed");
        await Within(ChannelClosed.Task, "player's channel closing");
    }

    private static async Task Within(Task task, string what)
    {
        if (await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(15))) != task)
            throw new TimeoutException("timed out waiting for " + what);
        await task;
    }

    private static async Task<T> Within<T>(Task<T> task, string what)
    {
        await Within((Task)task, what);
        return task.Result;
    }
}

using NordvikManager.Wasm.Host.SelfCheck;

// The browser-wasm GM host, in the page's worker (host-worker.js, which passes these arguments):
//   gm <centralUrl>                    the GM's host (HostPage)
//   selfcheck                          the self-check (SelfCheck/)
//   selfcheck crash-write|crash-check  storage-e2e.cjs (StorageCrashCheck)
switch (args)
{
    case ["gm", var central]:
        await NordvikManager.Wasm.Host.HostPage.RunAsync(central);
        break;
    case ["selfcheck", "crash-write"]:
        await StorageCrashCheck.WriteForeverAsync();
        break;
    case ["selfcheck", "crash-check"]:
        StorageCrashCheck.Check();
        break;
    case ["selfcheck"]:
        var passed = await HostSelfCheck.RunAsync();
        Console.WriteLine(passed ? "RESULT: PASS" : "RESULT: FAIL");
        break;
    default:
        Console.Error.WriteLine($"Unknown arguments: {string.Join(' ', args)}");
        break;
}

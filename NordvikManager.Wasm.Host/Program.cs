using NordvikManager.Wasm.Host.SelfCheck;
using System.Runtime.InteropServices.JavaScript;

// The browser-wasm GM host. ?selfcheck runs the self-check (SelfCheck/); hosting a real game
// (login, Central session, signaling) is not here yet.
var search = JSHost.GlobalThis.GetPropertyAsJSObject("location")?.GetPropertyAsString("search") ?? "";
if (search.Contains("selfcheck", StringComparison.OrdinalIgnoreCase))
{
    var passed = await HostSelfCheck.RunAsync();
    Console.WriteLine(passed ? "RESULT: PASS" : "RESULT: FAIL");
}
else
{
    Console.WriteLine("NordvikManager GM host: only the self-check (?selfcheck) is available so far.");
}

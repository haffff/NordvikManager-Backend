using NordvikManager.Wasm.Host.SelfCheck;
using System.Runtime.InteropServices.JavaScript;

// The browser-wasm GM host: the GM's page (HostPage: log in, games, start a session), or with
// ?selfcheck the self-check (SelfCheck/).
var search = JSHost.GlobalThis.GetPropertyAsJSObject("location")?.GetPropertyAsString("search") ?? "";
if (search.Contains("selfcheck", StringComparison.OrdinalIgnoreCase))
{
    var passed = await HostSelfCheck.RunAsync();
    Console.WriteLine(passed ? "RESULT: PASS" : "RESULT: FAIL");
}
else
{
    await NordvikManager.Wasm.Host.HostPage.RunAsync();
}

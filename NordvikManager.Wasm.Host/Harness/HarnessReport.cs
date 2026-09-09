using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace NordvikManager.Wasm.Host.Harness
{
    /// <summary>
    /// Collects every harness's pass/fail and posts ONE aggregate result to serve.js
    /// (written to harness-result.json) at the end of the run. Before this, only
    /// SessionServiceHarness — the last harness, which also calls window.close() — wrote a
    /// durable result, so GameLobbyHarness / CentralSignalingSpike / WebRTCSessionServiceHarness
    /// outcomes were console-only and lost the moment the tab closed. Reuses testSignaling.js's
    /// reportDone (already imported by the WebRTC harnesses) rather than adding another JS module.
    /// </summary>
    internal static partial class HarnessReport
    {
        private static readonly List<(string Name, bool Ok, string Message)> _results = new();

        public static void Record(string name, bool ok, string message)
        {
            _results.Add((name, ok, message));
            Console.WriteLine($"[HarnessReport] {name}: {(ok ? "PASS" : "FAIL")} — {message}");
        }

        /// <summary>
        /// Posts the aggregate (ok = every recorded harness passed) and, via reportDone,
        /// closes the tab. Safe to call once at the very end of the harness run.
        /// </summary>
        public static async Task FlushAsync()
        {
            await JSHost.ImportAsync("test-signaling", "/testSignaling.js");

            var allOk = _results.Count > 0 && _results.All(r => r.Ok);
            var summary = _results.Count == 0
                ? "no harness results recorded"
                : string.Join(" | ", _results.Select(r => $"{r.Name}={(r.Ok ? "PASS" : "FAIL")}: {r.Message}"));

            Console.WriteLine($"[HarnessReport] AGGREGATE {(allOk ? "PASS" : "FAIL")} — {summary}");
            ReportDone(allOk, summary);
        }

        [JSImport("reportDone", "test-signaling")]
        private static partial void ReportDone(bool ok, string message);
    }
}

#nullable enable
using System.Collections.Generic;
using System.Text;

namespace NordvikManager.Hosting
{
    // CentralReachable is null when no Central Server is configured; LogFolder is null when there
    // are no log files (Development logs to the console only).
    public record StartupInfo(
        string Version,
        IReadOnlyList<string> Urls,
        string CentralServerUrl,
        bool? CentralReachable,
        string DataFolder,
        string? LogFolder);

    /// <summary>
    /// What a GM sees in the console once the server is up: where to open it, whether players can
    /// reach it through the Central Server, and where their data is. Plain ASCII so every console
    /// (and code page) shows it the same.
    /// </summary>
    public static class StartupBanner
    {
        private const string Rule = "  ==============================================================";
        private const int LabelWidth = 24;

        // A crescent moon, the NordvikManager mark; the name and version sit beside it.
        private static readonly string[] Moon =
        {
            @"          _..._",
            @"        .'  .-'",
            @"       /   /",
            @"      |   |",
            @"      |   |",
            @"       \   \",
            @"        '.  '-.",
            @"          '---'",
        };

        public static string Format(StartupInfo info)
        {
            var text = new StringBuilder();
            var beside = new[] { "", "NordvikManager", $"ver {info.Version}", "", "Server is running." };
            text.AppendLine();
            for (var i = 0; i < Moon.Length; i++)
                text.AppendLine(i < beside.Length && beside[i] != "" ? Moon[i].PadRight(24) + beside[i] : Moon[i]);
            text.AppendLine();
            text.AppendLine(Rule);
            text.AppendLine();

            if (info.Urls.Count == 0)
                Line(text, "Open in your browser:", "(no address - check Kestrel in appsettings.json)");
            for (var i = 0; i < info.Urls.Count; i++)
                Line(text, i == 0 ? "Open in your browser:" : "", info.Urls[i]);
            text.AppendLine();

            switch (info.CentralReachable)
            {
                case true:
                    Line(text, "Central server:", $"{info.CentralServerUrl} (connected)");
                    break;
                case false:
                    Line(text, "Central server:", $"{info.CentralServerUrl} - NOT reachable!");
                    Line(text, "", "Players can't join until it is. Check your internet connection.");
                    break;
                default:
                    Line(text, "Central server:", "not configured (CentralServerUrl in appsettings.json)");
                    break;
            }
            Line(text, "Your data:", info.DataFolder);
            if (info.LogFolder != null)
                Line(text, "Logs:", info.LogFolder);
            text.AppendLine();
            text.AppendLine("    Keep this window open while you play.");
            text.AppendLine("    To stop the server, close this window or press Ctrl+C.");
            text.AppendLine();
            text.AppendLine(Rule);
            return text.ToString();
        }

        private static void Line(StringBuilder text, string label, string value) =>
            text.AppendLine("    " + label.PadRight(LabelWidth) + value);
    }
}

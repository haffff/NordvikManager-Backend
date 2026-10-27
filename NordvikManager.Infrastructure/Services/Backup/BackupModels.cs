using System.Text.Json.Serialization;

namespace NordvikManager.Infrastructure.Services.Backup
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BackupKind
    {
        Manual,
        Scheduled,
        // Taken automatically right before a restore is staged, so a restore can be undone.
        PreRestore,
        // Uploaded by the GM (e.g. a backup from another machine).
        Imported,
    }

    public record BackupInfo(string Name, BackupKind Kind, DateTime CreatedAtUtc, long SizeBytes, string? Label);

    public record PendingRestoreInfo(string BackupName, DateTime StagedAtUtc);

    public record LinkedResourceInfo(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("gameId")] string GameId,
        [property: JsonPropertyName("path")] string? Path);

    /// <summary>manifest.json at the root of every backup zip.</summary>
    public class BackupManifest
    {
        public const int CurrentFormatVersion = 1;

        [JsonPropertyName("formatVersion")] public int FormatVersion { get; set; }
        [JsonPropertyName("appVersion")] public string? AppVersion { get; set; }
        [JsonPropertyName("latestMigrationId")] public string? LatestMigrationId { get; set; }
        [JsonPropertyName("provider")] public string Provider { get; set; } = "";
        [JsonPropertyName("createdAtUtc")] public DateTime CreatedAtUtc { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; } = "";
        [JsonPropertyName("games")] public int Games { get; set; }
        [JsonPropertyName("resources")] public int Resources { get; set; }
        [JsonPropertyName("files")] public int Files { get; set; }
        // Linked resources point at files the app doesn't own, so they are not in the zip —
        // listed so the GM can see what a restore on another machine won't bring back.
        [JsonPropertyName("linkedResources")] public List<LinkedResourceInfo> LinkedResources { get; set; } = new();
    }

    public class BackupSchedule
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; set; }
        [JsonPropertyName("intervalHours")] public int IntervalHours { get; set; } = 24;
        [JsonPropertyName("retentionCount")] public int RetentionCount { get; set; } = 7;
        [JsonPropertyName("lastRunUtc")] public DateTime? LastRunUtc { get; set; }

        [JsonIgnore]
        public DateTime? NextRunUtc => !Enabled ? null : LastRunUtc?.AddHours(IntervalHours);

        public bool IsDue(DateTime nowUtc) =>
            Enabled && (LastRunUtc == null || nowUtc - LastRunUtc.Value >= TimeSpan.FromHours(IntervalHours));
    }

    /// <summary>A backup request that can't be done — bad name, invalid archive, etc. Shown to the GM.</summary>
    public class BackupException : Exception
    {
        public BackupException(string message, Exception? inner = null) : base(message, inner) { }
    }

    public class BackupNotFoundException : BackupException
    {
        public BackupNotFoundException(string name) : base($"Backup '{name}' not found.") { }
    }

    public class BackupNotSupportedException : BackupException
    {
        public BackupNotSupportedException() : base("Backups are only supported with the SQLite database.") { }
    }
}

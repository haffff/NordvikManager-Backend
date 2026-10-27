#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>Written by the old version after it swapped the files; the new version's first start reads it.</summary>
    public record PendingUpdate(string FromVersion, string ToVersion, string? BackupName, IReadOnlyList<string> Replaced);

    /// <summary>How the last update went, shown once by the version that runs afterwards.</summary>
    public record UpdateResult(string FromVersion, string ToVersion, bool Succeeded, string? Message, DateTime AtUtc);

    /// <summary>The files in data/updates that carry an update across the restart. Old and new versions both read them.</summary>
    public static class UpdateMarkers
    {
        private const string PendingFile = "pending-update.json";
        private const string ResultFile = "last-update.json";

        public static void WritePending(string folder, PendingUpdate pending) => Write(Path.Combine(folder, PendingFile), pending);

        public static PendingUpdate? ReadPending(string folder) => Read<PendingUpdate>(Path.Combine(folder, PendingFile));

        public static void DeletePending(string folder) => Delete(Path.Combine(folder, PendingFile));

        public static void WriteResult(string folder, UpdateResult result) => Write(Path.Combine(folder, ResultFile), result);

        /// <summary>Reads and removes the result, so it is shown once.</summary>
        public static UpdateResult? TakeResult(string folder)
        {
            var path = Path.Combine(folder, ResultFile);
            var result = Read<UpdateResult>(path);
            Delete(path);
            return result;
        }

        private static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value));
            File.Move(temp, path, overwrite: true);
        }

        private static T? Read<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null;
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static void Delete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}

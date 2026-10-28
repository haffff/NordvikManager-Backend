#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace NordvikManager.Hosting.Updates
{
    /// <summary>
    /// Swaps the program files of a release folder (exe, wwwroot, BuiltInAddons, README) for a
    /// new release's. The GM's appsettings, data/ and .localadmin are never touched. The replaced
    /// files are renamed to *.old (Windows lets a running exe be renamed, not overwritten), so a
    /// failed start can put them back.
    /// </summary>
    public static class UpdateInstaller
    {
        private const string OldSuffix = ".old";

        public static string ExecutableName => OperatingSystem.IsWindows() ? "NordvikManager.exe" : "NordvikManager";

        /// <summary>Unpacks a release zip into an empty staging folder and checks it holds the server.</summary>
        public static void Unpack(string zipPath, string stagingFolder, string executableName)
        {
            DeleteEntry(stagingFolder);
            Directory.CreateDirectory(stagingFolder);
            var root = Path.GetFullPath(stagingFolder) + Path.DirectorySeparatorChar;

            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                foreach (var entry in zip.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.Ordinal))
                        throw new UpdateException($"The release contains a file outside its folder ({entry.FullName}).");
                    if (entry.FullName.EndsWith('/'))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }
            catch (InvalidDataException error)
            {
                throw new UpdateException("The release zip can't be opened.", error);
            }

            var executable = Path.Combine(stagingFolder, executableName);
            if (!File.Exists(executable))
                throw new UpdateException($"The release doesn't contain {executableName}.");
            // Zips made on Windows (or by GitHub artifacts) don't keep the execute bit.
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(executable, File.GetUnixFileMode(executable)
                    | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }

        /// <summary>Moves the staged release into the app folder; returns the names it put there (for RollBack).</summary>
        public static IReadOnlyList<string> Apply(string appFolder, string stagingFolder)
        {
            var names = Directory.EnumerateFileSystemEntries(stagingFolder)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => !IsKept(name))
                .ToList();

            var placed = new List<string>();
            try
            {
                foreach (var name in names)
                {
                    var target = Path.Combine(appFolder, name);
                    var old = target + OldSuffix;
                    DeleteEntry(old);
                    if (Exists(target))
                        MoveEntry(target, old);
                    placed.Add(name);
                    MoveEntry(Path.Combine(stagingFolder, name), target);
                }
            }
            catch (Exception error)
            {
                RollBack(appFolder, placed);
                throw new UpdateException($"The new version couldn't be put in place: {error.Message}", error);
            }
            return names;
        }

        /// <summary>Puts back what Apply replaced, and removes what it added.</summary>
        public static void RollBack(string appFolder, IEnumerable<string> placed)
        {
            foreach (var name in placed)
            {
                var target = Path.Combine(appFolder, name);
                var old = target + OldSuffix;
                if (Exists(old))
                {
                    // A running exe can't be deleted on Windows, but it can be renamed out of the way.
                    if (Exists(target))
                    {
                        var failed = target + ".failed";
                        DeleteEntry(failed);
                        MoveEntry(target, failed);
                    }
                    MoveEntry(old, target);
                }
                else
                    DeleteEntry(target);
            }
        }

        /// <summary>Deletes the *.old (and *.failed) leftovers; ones still in use are left for the next start.</summary>
        public static void RemoveOld(string appFolder)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(appFolder)
                         .Where(p => p.EndsWith(OldSuffix, StringComparison.Ordinal) || p.EndsWith(".failed", StringComparison.Ordinal))
                         .ToList())
            {
                try
                {
                    DeleteEntry(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // The previous process may not have exited yet.
                }
            }
        }

        // The GM's own files, and what the release zips leave to them.
        private static bool IsKept(string name) =>
            name.Equals("data", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".localadmin", StringComparison.OrdinalIgnoreCase)
            || (name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

        private static void MoveEntry(string from, string to)
        {
            if (Directory.Exists(from))
                Directory.Move(from, to);
            else
                File.Move(from, to);
        }

        private static void DeleteEntry(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
    }
}

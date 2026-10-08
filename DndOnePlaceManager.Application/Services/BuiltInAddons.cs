using System.IO.Compression;
using System.Text.Json;

namespace DndOnePlaceManager.Application.Services
{
    /// <summary>An addon shipped with the server (from its info.json), packed like a release zip.</summary>
    public record BuiltInAddon(string Key, string? Name, string? Description, string? Version, string FileName, byte[] Data);

    /// <summary>
    /// Addons that ship with the server. They're offered when a game is created (ticked
    /// by default) and installed from this copy, so they work without reaching the addon
    /// registry. They stay ordinary addons afterwards (a GM can uninstall or update them).
    /// </summary>
    public interface IBuiltInAddons
    {
        IReadOnlyList<BuiltInAddon> GetAll();
    }

    /// <summary>
    /// Each folder under the root (by default BuiltInAddons/ next to the server) that has
    /// an info.json is one addon, laid out like an addon's addon_files folder. It's zipped
    /// in memory, with the same layout pnpm run pack produces.
    /// </summary>
    public class BuiltInAddons : IBuiltInAddons
    {
        private readonly string _root;

        public BuiltInAddons() : this(Path.Combine(AppContext.BaseDirectory, "BuiltInAddons"))
        {
        }

        public BuiltInAddons(string root)
        {
            _root = root;
        }

        // The files don't change while the server runs: read and zip them once.
        private IReadOnlyList<BuiltInAddon>? _loaded;

        public IReadOnlyList<BuiltInAddon> GetAll() => _loaded ??= Load();

        private IReadOnlyList<BuiltInAddon> Load()
        {
            if (!Directory.Exists(_root))
                return Array.Empty<BuiltInAddon>();

            var addons = new List<BuiltInAddon>();
            foreach (var dir in Directory.GetDirectories(_root).OrderBy(d => d, StringComparer.Ordinal))
            {
                var info = ReadInfo(Path.Combine(dir, "info.json"));
                if (info?.Key is not { Length: > 0 } key)
                    continue; // no info.json, or one the installer would reject anyway
                addons.Add(new BuiltInAddon(key, info.Name, info.Description, info.Version, Path.GetFileName(dir) + ".zip", Zip(dir)));
            }
            return addons;
        }

        private sealed record Info(string? Key, string? Name, string? Description, string? Version);

        private static Info? ReadInfo(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Info>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static byte[] Zip(string dir)
        {
            using var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    // "/" separators: the installer looks entries up as e.g. "resources/…".
                    var name = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, name);
                }
            }
            return buffer.ToArray();
        }
    }
}

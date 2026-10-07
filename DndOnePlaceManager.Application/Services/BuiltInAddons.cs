using System.IO.Compression;

namespace DndOnePlaceManager.Application.Services
{
    /// <summary>An addon shipped with the server, packed like a release zip.</summary>
    public record BuiltInAddon(string FileName, byte[] Data);

    /// <summary>
    /// Addons that ship with the server and are installed into every new game, so they
    /// work without reaching the addon registry. They stay ordinary addons afterwards
    /// (a GM can uninstall or update them).
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

        public IReadOnlyList<BuiltInAddon> GetAll()
        {
            if (!Directory.Exists(_root))
                return Array.Empty<BuiltInAddon>();

            return Directory.GetDirectories(_root)
                .Where(dir => File.Exists(Path.Combine(dir, "info.json")))
                .OrderBy(dir => dir, StringComparer.Ordinal)
                .Select(dir => new BuiltInAddon(Path.GetFileName(dir) + ".zip", Zip(dir)))
                .ToList();
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

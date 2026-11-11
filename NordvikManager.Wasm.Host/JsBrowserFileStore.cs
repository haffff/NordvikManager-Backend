using NordvikManager.Wasm.Hosting;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace NordvikManager.Wasm.Host
{
    /// <summary>
    /// <see cref="IBrowserFileStore"/> over IndexedDB (nordvik-storage.js): the browser host's
    /// database file between page loads, under a key (the GM page's games are "data.db").
    /// Call <see cref="InitializeAsync"/> first.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public sealed partial class JsBrowserFileStore : IBrowserFileStore
    {
        private const string Module = "nordvik-storage";
        private readonly string _key;

        public JsBrowserFileStore(string key = "data.db") => _key = key;

        public static Task InitializeAsync() => JSHost.ImportAsync(Module, "../nordvik-storage.js");

        public async Task<byte[]?> LoadAsync()
        {
            var length = await Load(_key);
            if (length <= 0)
                return null;
            var contents = new byte[length];
            CopyLoaded(contents);
            return contents;
        }

        public async Task SaveAsync(byte[] contents)
        {
            Stage(contents);
            await Commit(_key);
        }

        [JSImport("load", Module)]
        private static partial Task<int> Load(string key);

        [JSImport("copyLoaded", Module)]
        private static partial void CopyLoaded([JSMarshalAs<JSType.MemoryView>] Span<byte> target);

        [JSImport("stage", Module)]
        private static partial void Stage([JSMarshalAs<JSType.MemoryView>] Span<byte> contents);

        [JSImport("commit", Module)]
        private static partial Task Commit(string key);
    }
}

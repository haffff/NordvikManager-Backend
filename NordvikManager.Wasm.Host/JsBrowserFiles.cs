using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using NordvikManager.Wasm.Hosting;

namespace NordvikManager.Wasm.Host
{
    /// <summary><see cref="IBrowserFiles"/> on OPFS: opfs-files.js, in the host's worker.</summary>
    [SupportedOSPlatform("browser")]
    public sealed partial class JsBrowserFiles : IBrowserFiles
    {
        private const string Module = "opfs-files";
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        private JsBrowserFiles() { }

        public static async Task<JsBrowserFiles> CreateAsync()
        {
            await JSHost.ImportAsync(Module, "../opfs-files.js");
            return new JsBrowserFiles();
        }

        [JSImport("list", Module)]
        private static partial Task<string> List(string path);

        [JSImport("open", Module)]
        private static partial Task Open(string path, bool create);

        [JSImport("readAt", Module)]
        private static partial int Read(string path, [JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, double at);

        [JSImport("writeAt", Module)]
        private static partial void Write(string path, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, double at);

        [JSImport("truncate", Module)]
        private static partial void TruncateJs(string path, double size);

        [JSImport("flush", Module)]
        private static partial void FlushJs(string path);

        [JSImport("close", Module)]
        private static partial void CloseJs(string path);

        [JSImport("move", Module)]
        private static partial Task Move(string from, string to);

        [JSImport("remove", Module)]
        private static partial Task Remove(string path);

        [JSImport("modified", Module)]
        private static partial Task<double> Modified(string path);

        private static T Quota<T>(Func<T> call)
        {
            try
            {
                return call();
            }
            catch (JSException ex) when (ex.Message.Contains("quota:"))
            {
                throw new BrowserStorageFullException();
            }
        }

        public async Task<IReadOnlyList<BrowserFile>> ListAsync(string folder) =>
            JsonSerializer.Deserialize<List<BrowserFile>>(await List(folder), Json) ?? new List<BrowserFile>();

        public Task OpenAsync(string path, bool create) => Open(path, create);

        public int ReadAt(string path, Span<byte> buffer, long at) => Read(path, buffer, at);

        // MemoryView marshals a Span; the JS side only copies out of it.
        public void WriteAt(string path, ReadOnlySpan<byte> data, long at)
        {
            var span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(data), data.Length);
            try
            {
                Write(path, span, at);
            }
            catch (JSException ex) when (ex.Message.Contains("quota:"))
            {
                throw new BrowserStorageFullException();
            }
        }

        public void Truncate(string path, long size) => Quota(() => { TruncateJs(path, size); return 0; });

        public void Flush(string path) => FlushJs(path);

        public void Close(string path) => CloseJs(path);

        public Task MoveAsync(string from, string to) => Move(from, to);

        public Task DeleteAsync(string path) => Remove(path);

        public Task<double> ModifiedAsync(string path) => Modified(path);
    }
}

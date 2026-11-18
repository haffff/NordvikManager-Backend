namespace NordvikManager.Wasm.Hosting
{
    /// <summary>A file kept by the browser (OPFS): its path, size, and last change (ms since 1970).</summary>
    public record BrowserFile(string Path, long Size, double Modified);

    /// <summary>The browser couldn't make room for a file (OPFS's QuotaExceededError).</summary>
    public class BrowserStorageFullException : IOException
    {
        public BrowserStorageFullException() : base("The browser has no room left for this file.") { }
    }

    /// <summary>
    /// The browser's own files (OPFS, in the host's worker: NordvikManager.Wasm.Host's
    /// opfs-files.js), for <see cref="BrowserFileStorage"/>. Paths are relative to OPFS's root,
    /// with '/'. A file is opened once (<see cref="OpenAsync"/>) and its handle kept: the sync
    /// calls need it open. Moving or deleting a file lets go of its handle.
    /// </summary>
    public interface IBrowserFiles
    {
        /// <summary>Every file under <paramref name="folder"/>, at any depth.</summary>
        Task<IReadOnlyList<BrowserFile>> ListAsync(string folder);

        /// <summary>Opens a file (and its folders, with <paramref name="create"/>); already open is fine.</summary>
        Task OpenAsync(string path, bool create);

        int ReadAt(string path, Span<byte> buffer, long at);

        /// <exception cref="BrowserStorageFullException">No room left.</exception>
        void WriteAt(string path, ReadOnlySpan<byte> data, long at);

        void Truncate(string path, long size);
        void Flush(string path);
        void Close(string path);

        /// <summary>Puts <paramref name="from"/> at <paramref name="to"/>, replacing what is there.</summary>
        Task MoveAsync(string from, string to);

        /// <summary>Deletes a file; one that isn't there is fine.</summary>
        Task DeleteAsync(string path);

        /// <summary>When the file last changed, as <see cref="ListAsync"/> will say after a reload.</summary>
        Task<double> ModifiedAsync(string path);
    }
}

namespace DndOnePlaceManager.Infrastructure.Interfaces
{
    /// <summary>
    /// Provides access to the configured addon repository.
    /// All URLs are resolved internally — no external URL is ever accepted from callers.
    /// </summary>
    public interface IAddonRepositoryService
    {
        /// <summary>Downloads the addon zip by its registry key.</summary>
        Task<byte[]> GetAddonByKey(string key);

        /// <summary>Returns the raw repository JSON from the configured MainRepository.</summary>
        Task<string> GetRepository();

        /// <summary>
        /// The markdown readme of the registry entry with this key (its "readme" URL), or null
        /// when the entry has none or there is no such key. Throws InvalidOperationException
        /// when the URL isn't a markdown file on raw.githubusercontent.com, or it can't be read.
        /// </summary>
        Task<AddonReadmeResult?> GetReadmeByKey(string key);
    }

    /// <summary>A readme and the URL it came from.</summary>
    public record AddonReadmeResult(string Url, string Markdown);
}

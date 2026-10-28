#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace NordvikManager.Hosting.Updates
{
    public class UpdateException : Exception
    {
        public UpdateException(string message, Exception? inner = null) : base(message, inner) { }
    }

    /// <summary>Downloads a release zip and checks it against the checksum GitHub published for it.</summary>
    public static class UpdateDownloader
    {
        public static async Task DownloadAsync(HttpClient client, UpdateOffer offer, string targetFile, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetFile))!);
            try
            {
                using var response = await client.GetAsync(offer.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new UpdateException($"The download failed: the server answered {(int)response.StatusCode}.");

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var target = File.Create(targetFile))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        total += read;
                        if (total > offer.Size)
                            throw new UpdateException("The download is larger than the release says it is.");
                        hash.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        progress?.Report(total);
                    }
                }

                var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (actual != offer.Sha256)
                    throw new UpdateException("The download is damaged (its checksum doesn't match the release). Try again later.");
            }
            catch (Exception error)
            {
                TryDelete(targetFile);
                if (error is UpdateException or OperationCanceledException)
                    throw;
                throw new UpdateException($"The download failed: {error.Message}", error);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }
}

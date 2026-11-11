using NordvikManager.Domain.Enums;

namespace NordvikManager.Application.Helpers
{
    internal static class ThumbnailHelper
    {
        // Only these MimeType values are actual raster images a thumbnail generator can decode —
        // everything else (audio, html, json, ...) has no thumbnail to generate.
        public static bool IsImage(MimeType mimeType) =>
            mimeType == MimeType.PNG || mimeType == MimeType.JPEG ||
            mimeType == MimeType.GIF || mimeType == MimeType.TIFF;
    }

    /// <summary>
    /// Makes resource thumbnails. Provided by the host, so Application carries no imaging
    /// library: the native server uses SkiaSharp (NordvikManager.Infrastructure.Imaging). A host that registers
    /// none gets <see cref="NoThumbnailGenerator"/>, and resources are served at full size.
    /// </summary>
    public interface IThumbnailGenerator
    {
        /// <summary>
        /// Resizes the given image bytes to fit within maxDimension x maxDimension
        /// (preserving aspect ratio, never upscaling) and re-encodes as PNG so
        /// transparency survives regardless of the source format. Throws on data it can't decode;
        /// null when this host makes no thumbnails at all.
        /// </summary>
        byte[]? Generate(byte[] sourceBytes, int maxDimension);
    }

    /// <summary>The default for a host without an imaging library: no thumbnails.</summary>
    internal sealed class NoThumbnailGenerator : IThumbnailGenerator
    {
        public byte[]? Generate(byte[] sourceBytes, int maxDimension) => null;
    }
}

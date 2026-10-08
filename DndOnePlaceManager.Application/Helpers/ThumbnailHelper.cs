using DndOnePlaceManager.Domain.Enums;
using SkiaSharp;

namespace DndOnePlaceManager.Application.Helpers
{
    internal static class ThumbnailHelper
    {
        // Only these MimeType values are actual raster images SkiaSharp can decode —
        // everything else (audio, html, json, ...) has no thumbnail to generate.
        public static bool IsImage(MimeType mimeType) =>
            mimeType == MimeType.PNG || mimeType == MimeType.JPEG ||
            mimeType == MimeType.GIF || mimeType == MimeType.TIFF;

        /// <summary>
        /// Resizes the given image bytes to fit within maxDimension x maxDimension
        /// (preserving aspect ratio, never upscaling) and re-encodes as PNG so
        /// transparency survives regardless of the source format.
        /// </summary>
        public static byte[] Generate(byte[] sourceBytes, int maxDimension)
        {
            using var original = SKBitmap.Decode(sourceBytes)
                ?? throw new InvalidOperationException("Could not decode image data.");

            var scale = Math.Min(1f, (float)maxDimension / Math.Max(original.Width, original.Height));
            var width = Math.Max(1, (int)Math.Round(original.Width * scale));
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            using var resized = scale < 1f
                ? original.Resize(new SKImageInfo(width, height), SKSamplingOptions.Default)
                : original;
            if (resized == null)
                throw new InvalidOperationException("Failed to resize image.");

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
    }
}

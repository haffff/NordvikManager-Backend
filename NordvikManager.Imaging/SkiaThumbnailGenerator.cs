using NordvikManager.Application.Helpers;
using SkiaSharp;

namespace NordvikManager.Imaging
{
    /// <summary>Resource thumbnails with SkiaSharp.</summary>
    public sealed class SkiaThumbnailGenerator : IThumbnailGenerator
    {
        public byte[]? Generate(byte[] sourceBytes, int maxDimension)
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

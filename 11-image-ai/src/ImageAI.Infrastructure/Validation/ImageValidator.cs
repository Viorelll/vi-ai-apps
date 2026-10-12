using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Models;
using SkiaSharp;

namespace ImageAI.Infrastructure.Validation;

public static class ImageValidator
{
    public const int MaxBytes = 50 * 1024 * 1024;
    public const int MaxPixels = 20_000_000;

    public static ImageInfo Inspect(byte[] bytes, bool mask = false)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes)
            throw new DomainException("file_size_limit", 413);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
            throw new DomainException("invalid_image");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
            throw new DomainException("image_png_jpeg_required");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > MaxPixels)
            throw new DomainException("image_pixel_limit");
        if (codec.FrameCount > 1)
            throw new DomainException("animated_image_not_supported");
        using var bitmap = new SKBitmap(
            new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
            throw new DomainException("invalid_image");
        bool alpha =
            codec.EncodedFormat == SKEncodedImageFormat.Png
            && bytes.Length > 25
            && bytes[25] is 4 or 6;
        if (mask)
        {
            if (!alpha)
                throw new DomainException("mask_png_alpha_required");
            bool selected = false,
                protectedPixel = false;
            foreach (var pixel in bitmap.Pixels)
            {
                selected |= pixel.Alpha < 255;
                protectedPixel |= pixel.Alpha == 255;
            }
            if (!selected || !protectedPixel)
                throw new DomainException("mask_selection_required");
        }
        return new(
            info.Width,
            info.Height,
            alpha,
            codec.EncodedFormat == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg"
        );
    }

    public static void ValidateMask(int width, int height, byte[] bytes)
    {
        var info = Inspect(bytes, true);
        if (info.Width != width || info.Height != height)
            throw new DomainException("mask_size_mismatch");
    }
}

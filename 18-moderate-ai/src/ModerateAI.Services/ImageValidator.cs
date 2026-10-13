using ModerateAI.Domain.Models;
using SkiaSharp;

namespace ModerateAI.Services;

public static class ImageValidator
{
    public const int MaximumBytes = 4 * 1024 * 1024;

    public static string Validate(byte[] bytes, string name)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumBytes)
            throw new DomainException(413, "image_size_limit_4_mib");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null)
            throw new DomainException(415, "invalid_image_content");
        string ext = Path.GetExtension(name).ToLowerInvariant();
        string mime = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png when ext == ".png" => "image/png",
            SKEncodedImageFormat.Jpeg when ext is ".jpg" or ".jpeg" => "image/jpeg",
            _ => throw new DomainException(415, "supported_images_png_jpeg"),
        };
        var info = codec.Info;
        if (info.Width < 50 || info.Height < 50 || info.Width > 7200 || info.Height > 7200)
            throw new DomainException(400, "image_dimensions_50_to_7200");
        using var bitmap = new SKBitmap(
            new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new DomainException(415, "invalid_image_content");
        return mime;
    }
}

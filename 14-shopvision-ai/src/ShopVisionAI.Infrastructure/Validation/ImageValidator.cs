namespace ShopVisionAI.Infrastructure.Validation;

public static class ImageValidator
{
    public const long MaxBytes = 50L * 1024 * 1024;

    public static string Detect(ReadOnlySpan<byte> bytes, string fileName, long size)
    {
        if (size <= 0 || size > MaxBytes)
            throw new ArgumentException("invalid_file_size");
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (
            bytes.Length >= 8
            && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && ext == ".png"
        )
            return "image/png";
        if (
            bytes.Length >= 3
            && bytes[0] == 255
            && bytes[1] == 216
            && bytes[2] == 255
            && ext is ".jpg" or ".jpeg"
        )
            return "image/jpeg";
        if (
            bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)
            && ext == ".webp"
        )
            return "image/webp";
        throw new ArgumentException("unsupported_image");
    }
}

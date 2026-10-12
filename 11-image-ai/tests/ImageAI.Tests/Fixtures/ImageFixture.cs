using SkiaSharp;

namespace ImageAI.Tests.Fixtures;

public static class ImageFixture
{
    public static byte[] Png(int width = 32, int height = 32, bool mask = false)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        bitmap.Erase(SKColors.White);
        if (mask)
            bitmap.SetPixel(0, 0, SKColors.Transparent);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

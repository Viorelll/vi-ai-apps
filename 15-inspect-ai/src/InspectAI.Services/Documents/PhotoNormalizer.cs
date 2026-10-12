using InspectAI.Domain.Models;
using SkiaSharp;

namespace InspectAI.Services.Documents;

public static class PhotoNormalizer
{
    public static Task WriteAsync(Stream input, Stream output, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var managed = new SKManagedStream(input, false);
        using var codec = SKCodec.Create(managed);
        if (codec is null)
            throw new AppException(415, "invalid_image", "Image cannot be decoded.");
        var info = codec.Info;
        if ((long)info.Width * info.Height > 16000000 || info.Width < 1 || info.Height < 1)
            throw new AppException(
                413,
                "image_pixel_limit",
                "Images must contain at most 16 million pixels."
            );
        using var decoded = new SKBitmap(
            new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul)
        );
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new AppException(415, "invalid_image", "Incomplete or corrupt image.");
        var swap = (int)codec.EncodedOrigin >= 5;
        using var normalized = new SKBitmap(
            swap ? info.Height : info.Width,
            swap ? info.Width : info.Height
        );
        using (var canvas = new SKCanvas(normalized))
        {
            float w = info.Width,
                h = info.Height;
            var matrix = codec.EncodedOrigin switch
            {
                SKEncodedOrigin.TopRight => Matrix(-1, 0, w, 0, 1, 0),
                SKEncodedOrigin.BottomRight => Matrix(-1, 0, w, 0, -1, h),
                SKEncodedOrigin.BottomLeft => Matrix(1, 0, 0, 0, -1, h),
                SKEncodedOrigin.LeftTop => Matrix(0, 1, 0, 1, 0, 0),
                SKEncodedOrigin.RightTop => Matrix(0, -1, h, 1, 0, 0),
                SKEncodedOrigin.RightBottom => Matrix(0, -1, h, -1, 0, w),
                SKEncodedOrigin.LeftBottom => Matrix(0, 1, 0, -1, 0, w),
                _ => SKMatrix.Identity,
            };
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(decoded, 0, 0);
            canvas.Flush();
        }
        using var image = SKImage.FromBitmap(normalized);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(output);
        output.Position = 0;
        input.Position = 0;
        if (output.Length > 20 * 1024 * 1024)
            throw new AppException(
                413,
                "normalized_image_limit",
                "Normalized image exceeds 20 MiB; upload a smaller photograph."
            );
        return Task.CompletedTask;
    }

    private static SKMatrix Matrix(float a, float b, float c, float d, float e, float f) =>
        new()
        {
            ScaleX = a,
            SkewX = b,
            TransX = c,
            SkewY = d,
            ScaleY = e,
            TransY = f,
            Persp2 = 1,
        };
}

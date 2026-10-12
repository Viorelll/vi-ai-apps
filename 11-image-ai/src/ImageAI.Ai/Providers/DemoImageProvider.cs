using System.Security.Cryptography;
using System.Text;
using ImageAI.Contracts.Dto;
using SkiaSharp;

namespace ImageAI.Ai.Providers;

// Explicit deterministic laboratory provider. Never a fallback for failed Azure calls.
public sealed class DemoImageProvider : IImageProvider
{
    public Task<GatewayResult> GenerateAsync(GatewayRequest request, CancellationToken ct)
    {
        var parts = request.Size.Split('x');
        int width = int.Parse(parts[0]),
            height = int.Parse(parts[1]);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Prompt));
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        using var input = request.ImageBase64 is null
            ? null
            : SKBitmap.Decode(Convert.FromBase64String(request.ImageBase64));
        using var canvas = new SKCanvas(bitmap);
        if (input is not null)
            canvas.DrawBitmap(
                input,
                new SKRect(0, 0, width, height),
                new SKSamplingOptions(SKFilterMode.Linear)
            );
        using var mask = request.MaskBase64 is null
            ? null
            : SKBitmap.Decode(Convert.FromBase64String(request.MaskBase64));
        for (int y = 0; y < height; y++)
        {
            ct.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                if (mask is not null && mask.GetPixel(x, y).Alpha == 255)
                    continue;
                var dx = (x - width / 2f) / width;
                var dy = (y - height / 2f) / height;
                var product = dx * dx / 0.035f + dy * dy / 0.1f < 1;
                bitmap.SetPixel(
                    x,
                    y,
                    product
                        ? new SKColor((byte)(220 + hash[0] % 30), (byte)(220 + hash[1] % 30), 240)
                        : new SKColor(
                            (byte)(hash[0] / 2 + x * 80 / width),
                            (byte)(hash[1] / 2 + y * 80 / height),
                            (byte)(hash[2] / 2 + 40)
                        )
                );
            }
        }
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return Task.FromResult(
            new GatewayResult(
                Convert.ToBase64String(encoded.ToArray()),
                "demo-deterministic-not-ai",
                null,
                request.OperationId.ToString(),
                "DemoNotEvaluated"
            )
        );
    }
}

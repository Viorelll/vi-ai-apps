using InspectAI.Domain.Models;
using InspectAI.Services.Documents;
using SkiaSharp;

namespace InspectAI.Tests.Unit;

public sealed class PhotoTests
{
    [Fact]
    public async Task NormalizesExifBeforeVisionAndPreview()
    {
        await using var input = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "seeds", "orientation-test.jpg")
        );
        using var output = new MemoryStream();
        await PhotoNormalizer.WriteAsync(input, output, default);
        using var bitmap = SKBitmap.Decode(output.ToArray());
        Assert.Equal(40, bitmap.Width);
        Assert.Equal(60, bitmap.Height);
        output.Position = 0;
        using var codec = SKCodec.Create(output);
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
    }

    [Fact]
    public async Task RejectsSignatureOnlyImage()
    {
        using var input = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        using var output = new MemoryStream();
        await Assert.ThrowsAsync<AppException>(() =>
            PhotoNormalizer.WriteAsync(input, output, default)
        );
    }
}

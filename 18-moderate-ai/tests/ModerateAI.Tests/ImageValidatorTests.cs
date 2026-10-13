using ModerateAI.Domain.Models;
using ModerateAI.Services;
using SkiaSharp;
using Xunit;

namespace ModerateAI.Tests;

public sealed class ImageValidatorTests
{
    [Fact]
    public void ValidPngAcceptedAndWrongExtensionRejected()
    {
        using var image = new SKBitmap(80, 80);
        using var buffer = new MemoryStream();
        using var encoded = SKImage.FromBitmap(image).Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(buffer);
        Assert.Equal("image/png", ImageValidator.Validate(buffer.ToArray(), "fixture.png"));
        Assert.Throws<DomainException>(() =>
            ImageValidator.Validate(buffer.ToArray(), "fixture.jpg")
        );
    }

    [Fact]
    public void UndersizedImageRejected()
    {
        using var image = new SKBitmap(10, 10);
        using var buffer = new MemoryStream();
        using var encoded = SKImage.FromBitmap(image).Encode(SKEncodedImageFormat.Png, 100);
        encoded.SaveTo(buffer);
        Assert.Throws<DomainException>(() =>
            ImageValidator.Validate(buffer.ToArray(), "fixture.png")
        );
    }

    [Fact]
    public void OversizedImageRejected()
    {
        Assert.Equal(
            413,
            Assert
                .Throws<DomainException>(() =>
                    ImageValidator.Validate(
                        new byte[ImageValidator.MaximumBytes + 1],
                        "fixture.png"
                    )
                )
                .Status
        );
    }

    [Fact]
    public void TruncatedPngHeaderCannotPass()
    {
        var bytes = new byte[40];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        bytes[19] = 80;
        bytes[23] = 80;
        Assert.Throws<DomainException>(() => ImageValidator.Validate(bytes, "fake.png"));
    }
}

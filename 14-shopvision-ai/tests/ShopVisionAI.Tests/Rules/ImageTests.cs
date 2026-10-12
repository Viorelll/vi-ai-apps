using ShopVisionAI.Infrastructure.Validation;

namespace ShopVisionAI.Tests.Rules;

public sealed class ImageTests
{
    [Fact]
    public void ValidPng() =>
        Assert.Equal(
            "image/png",
            ImageValidator.Detect([137, 80, 78, 71, 13, 10, 26, 10], "seed.png", 100)
        );

    [Fact]
    public void SpoofRejected() =>
        Assert.Throws<ArgumentException>(() =>
            ImageValidator.Detect("not an image"u8, "seed.png", 100)
        );

    [Fact]
    public void ExtensionMismatchRejected() =>
        Assert.Throws<ArgumentException>(() =>
            ImageValidator.Detect([255, 216, 255], "seed.png", 100)
        );

    [Fact]
    public void OversizeRejected() =>
        Assert.Throws<ArgumentException>(() =>
            ImageValidator.Detect([255, 216, 255], "seed.jpg", ImageValidator.MaxBytes + 1)
        );
}

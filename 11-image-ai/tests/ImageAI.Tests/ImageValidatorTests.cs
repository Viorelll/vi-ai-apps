using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Validation;
using ImageAI.Tests.Fixtures;
using Xunit;

namespace ImageAI.Tests;

public sealed class ImageValidatorTests
{
    [Fact]
    public void ReadsRealDimensions()
    {
        var info = ImageValidator.Inspect(ImageFixture.Png(32, 64));
        Assert.Equal(32, info.Width);
        Assert.Equal(64, info.Height);
        Assert.Equal("image/png", info.ContentType);
    }

    [Fact]
    public void AllowsPartialAlphaMask() =>
        ImageValidator.ValidateMask(32, 32, ImageFixture.Png(mask: true));

    [Fact]
    public void RejectsMismatchedMask() =>
        Assert.Equal(
            "mask_size_mismatch",
            Assert
                .Throws<DomainException>(() =>
                    ImageValidator.ValidateMask(64, 32, ImageFixture.Png(mask: true))
                )
                .Code
        );

    [Fact]
    public void RejectsEmptySelection() =>
        Assert.Throws<DomainException>(() => ImageValidator.Inspect(ImageFixture.Png(), true));

    [Fact]
    public void RejectsForgedImage() =>
        Assert.Throws<DomainException>(() => ImageValidator.Inspect("not an image"u8.ToArray()));

    [Fact]
    public void RejectsTruncatedImage()
    {
        var bytes = ImageFixture.Png();
        Assert.Throws<DomainException>(() => ImageValidator.Inspect(bytes[..30]));
    }

    [Fact]
    public void RejectsEmptyUpload() =>
        Assert.Equal(413, Assert.Throws<DomainException>(() => ImageValidator.Inspect([])).Status);
}

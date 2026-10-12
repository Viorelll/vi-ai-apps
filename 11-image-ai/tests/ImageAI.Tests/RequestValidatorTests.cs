using ImageAI.Domain.Models;
using ImageAI.Services.Validation;
using Xunit;

namespace ImageAI.Tests;

public sealed class RequestValidatorTests
{
    [Theory]
    [InlineData("1024x1024", "medium")]
    [InlineData("1536x1024", "high")]
    [InlineData("1024x1536", "low")]
    public void AllowsDocumentedParameters(string size, string quality) =>
        RequestValidator.Parameters(size, quality);

    [Theory]
    [InlineData("8192x8192", "medium")]
    [InlineData("1024x1024", "ultra")]
    public void RejectsUnsupportedParameters(string size, string quality) =>
        Assert.Throws<DomainException>(() => RequestValidator.Parameters(size, quality));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void RejectsShortBrief(string brief) =>
        Assert.Throws<DomainException>(() => RequestValidator.Brief(brief));

    [Fact]
    public void NormalizesWhitespace() =>
        Assert.Equal("A product brief", RequestValidator.Brief("  A product brief  "));

    [Fact]
    public void RejectsUnboundedBrief() =>
        Assert.Throws<DomainException>(() => RequestValidator.Brief(new string('x', 4001)));

    [Fact]
    public void RequiresStableRequestId() =>
        Assert.Throws<DomainException>(() => RequestValidator.RequestId(Guid.Empty));
}

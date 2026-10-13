using System.Text;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Infrastructure;
using Xunit;

namespace BuildBudgetAI.Tests;

public sealed class UploadValidatorTests
{
    [Fact]
    public void PdfRequiresMagicAndExtension()
    {
        Assert.Equal("application/pdf", UploadValidator.Detect("%PDF-1.7"u8, "offer.pdf"));
        Assert.Throws<DomainException>(() => UploadValidator.Detect("not a PDF"u8, "offer.pdf"));
        Assert.Throws<DomainException>(() => UploadValidator.Detect("%PDF-1.7"u8, "offer.exe"));
    }

    [Fact]
    public void TextRejectsBinary() =>
        Assert.Throws<DomainException>(() =>
            UploadValidator.Detect(new byte[] { 0, 1 }, "offer.txt")
        );

    [Fact]
    public void ValidImageAccepted() =>
        Assert.Equal(
            "image/png",
            UploadValidator.Detect(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, "photo.png")
        );
}

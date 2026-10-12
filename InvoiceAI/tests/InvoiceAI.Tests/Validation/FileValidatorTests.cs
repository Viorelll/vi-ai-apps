using InvoiceAI.Services.Validation;
using InvoiceAI.Services.Exceptions;
namespace InvoiceAI.Tests.Validation;

public sealed class FileValidatorTests
{
    [Fact] public void PdfMagicMatches() => Assert.Equal("application/pdf", FileValidator.Detect("%PDF-1.4"u8, "invoice.pdf", "application/pdf"));
    [Theory]
    [InlineData("evil.exe", "application/pdf", "%PDF-")]
    [InlineData("invoice.pdf", "image/png", "%PDF-")]
    [InlineData("invoice.pdf", "application/pdf", "<html>")]
    public void RejectSpoofedTypes(string file, string type, string bytes) => Assert.Throws<AppException>(() => FileValidator.Detect(System.Text.Encoding.UTF8.GetBytes(bytes), file, type));
    [Fact] public void PngMagicMatches() => Assert.Equal("image/png", FileValidator.Detect([137, 80, 78, 71, 13, 10, 26, 10], "scan.png", "image/png"));
}

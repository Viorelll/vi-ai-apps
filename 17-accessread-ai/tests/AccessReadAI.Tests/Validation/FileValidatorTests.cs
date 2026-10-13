using System.Text;
using AccessReadAI.Contracts;
using AccessReadAI.Services.Validation;

namespace AccessReadAI.Tests.Validation;

public sealed class FileValidatorTests
{
    [Theory]
    [InlineData("a.pdf", "%PDF-1.7", "application/pdf")]
    [InlineData("a.txt", "Nu depășiți 10 unități.", "text/plain")]
    [InlineData("a.md", "# Heading", "text/plain")]
    public void ChecksSignature(string filename, string text, string type) =>
        Assert.Equal(type, FileValidator.Detect(filename, Encoding.UTF8.GetBytes(text)));

    [Theory]
    [InlineData("a.pdf", "hello")]
    [InlineData("a.exe", "MZ")]
    [InlineData("a.png", "hello")]
    public void RejectsMismatchedContent(string filename, string content) =>
        Assert.Throws<ApiFault>(() =>
            FileValidator.Detect(filename, Encoding.UTF8.GetBytes(content))
        );

    [Fact]
    public void RejectsNullByteText() =>
        Assert.Throws<ApiFault>(() => FileValidator.Detect("a.txt", new byte[] { 65, 0 }));
}

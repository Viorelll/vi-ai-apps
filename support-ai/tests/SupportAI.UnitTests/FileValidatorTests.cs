using System.Text;
using SupportAI.Application.Services;
using SupportAI.Domain.Models;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class FileValidatorTests
{
    [Theory]
    [InlineData("note.txt", "hello", "text/plain")]
    [InlineData("policy.md", "# Policy", "text/plain")]
    [InlineData("file.pdf", "%PDF-1.7", "application/pdf")]
    public void AcceptsKnownSignatures(string name, string content, string expected) => Assert.Equal(expected, FileValidator.Validate(name, Encoding.UTF8.GetBytes(content)));
    [Theory]
    [InlineData("fake.png", "hello")]
    [InlineData("evil.exe", "MZ")]
    [InlineData("fake.pdf", "not pdf")]
    public void RejectsMismatchedOrUnsupportedFiles(string name, string content) => Assert.Throws<SupportException>(() => FileValidator.Validate(name, Encoding.UTF8.GetBytes(content)));
    [Fact] public void RejectsBinaryAsText() => Assert.Throws<SupportException>(() => FileValidator.Validate("file.txt", new byte[] { 0, 1, 2 }));
}

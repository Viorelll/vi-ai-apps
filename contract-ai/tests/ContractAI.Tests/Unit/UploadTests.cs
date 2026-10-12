using System.Text;
using ContractAI.Domain.Models;
using ContractAI.Services.Documents;

namespace ContractAI.Tests.Unit;

public sealed class UploadTests
{
    [Theory]
    [InlineData("contract.pdf", "plain text")]
    [InlineData("contract.exe", "%PDF-")]
    [InlineData("contract.docx", "PK00")]
    [InlineData("contract.txt", "")]
    public async Task RejectsMismatchedOrUnsupportedFiles(string name, string contents)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(contents));
        await Assert.ThrowsAsync<AppException>(() =>
            FileValidator.ValidateAsync(name, stream, default)
        );
    }

    [Fact]
    public async Task AcceptsPdfSignature()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\nseed"));
        Assert.Equal(
            "application/pdf",
            await FileValidator.ValidateAsync("contract.pdf", stream, default)
        );
    }

    [Fact]
    public async Task RejectsInvalidUtf8()
    {
        using var stream = new MemoryStream(new byte[] { 0xff, 0xff });
        await Assert.ThrowsAsync<AppException>(() =>
            FileValidator.ValidateAsync("contract.txt", stream, default)
        );
    }

    [Fact]
    public async Task AcceptsRomanianText()
    {
        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("Plata în 15 zile de la recepție.")
        );
        Assert.Equal(
            "text/plain",
            await FileValidator.ValidateAsync("contract.txt", stream, default)
        );
    }
}

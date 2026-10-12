using System.Text;
using InspectAI.Domain.Models;
using InspectAI.Services.Documents;

namespace InspectAI.Tests.Unit;

public sealed class UploadTests
{
    [Fact]
    public async Task RejectsRenamedExecutable()
    {
        using var f = new MemoryStream(Encoding.UTF8.GetBytes("MZ executable"));
        await Assert.ThrowsAsync<AppException>(() =>
            FileValidator.ValidateAsync("image.png", f, default)
        );
    }

    [Fact]
    public async Task RejectsNulText()
    {
        using var f = new MemoryStream(Encoding.UTF8.GetBytes("text\0text"));
        await Assert.ThrowsAsync<AppException>(() =>
            FileValidator.ValidateAsync("manual.txt", f, default)
        );
    }

    [Fact]
    public async Task RejectsInvalidUtf8()
    {
        using var f = new MemoryStream(new byte[] { 0xff, 0xff });
        await Assert.ThrowsAsync<AppException>(() =>
            FileValidator.ValidateAsync("manual.txt", f, default)
        );
    }

    [Fact]
    public async Task AcceptsTextAndRewinds()
    {
        using var f = new MemoryStream(Encoding.UTF8.GetBytes("Inspect the stain."));
        Assert.Equal("text/plain", await FileValidator.ValidateAsync("manual.txt", f, default));
        Assert.Equal(0, f.Position);
    }
}

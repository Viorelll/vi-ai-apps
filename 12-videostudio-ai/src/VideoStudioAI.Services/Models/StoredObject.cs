namespace VideoStudioAI.Services.Models;

public sealed record StoredObject(string Path, string Sha256, long Length, string ContentType)
    : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        File.Delete(Path);
        return ValueTask.CompletedTask;
    }
}

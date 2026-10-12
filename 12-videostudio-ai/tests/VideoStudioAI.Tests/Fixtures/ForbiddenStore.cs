using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Tests.Fixtures;

public sealed class ForbiddenStore : IObjectStore
{
    public Task EnsureBucketAsync(CancellationToken ct) => throw new InvalidOperationException();

    public Task PutAsync(string key, string path, string type, CancellationToken ct) =>
        throw new InvalidOperationException();

    public Task<StoredObject> DownloadAsync(string key, CancellationToken ct) =>
        throw new InvalidOperationException();

    public Task DeleteAsync(string key, CancellationToken ct) =>
        throw new InvalidOperationException();
}

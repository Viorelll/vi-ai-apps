using ModerateAI.Domain.Interfaces;

namespace ModerateAI.Tests;

public sealed class TestObjectStore : IObjectStore
{
    public Task EnsureBucketAsync(CancellationToken ct) => Task.CompletedTask;

    public Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<byte[]> ReadAsync(string key, CancellationToken ct) =>
        Task.FromResult(new byte[] { 1, 2, 3 });

    public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;

    public Task<bool> ReadyAsync(CancellationToken ct) => Task.FromResult(true);
}

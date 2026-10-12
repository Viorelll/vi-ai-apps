using InspectAI.Infrastructure.Abstractions;

namespace InspectAI.Tests.Fakes;

public sealed class MemoryObjectStore : IObjectStore
{
    public Task EnsureBucketAsync(CancellationToken ct) => Task.CompletedTask;

    public Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct) =>
        Task.FromResult<Stream>(
            new MemoryStream(
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zk8sAAAAASUVORK5CYII="
                )
            )
        );

    public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
}

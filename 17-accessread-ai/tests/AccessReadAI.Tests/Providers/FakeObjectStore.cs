using AccessReadAI.Infrastructure.Storage;
using Amazon.S3.Model;

namespace AccessReadAI.Tests.Providers;

public sealed class FakeObjectStore(byte[] original) : IObjectStore
{
    public Task EnsureBucketAsync(CancellationToken ct) => Task.CompletedTask;

    public Task PutAsync(string key, Stream stream, string type, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<GetObjectResponse> OpenAsync(string key, CancellationToken ct) =>
        Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream(original) });

    public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;

    public Task PutBytesAsync(string key, byte[] bytes, string type, CancellationToken ct) =>
        Task.CompletedTask;
}

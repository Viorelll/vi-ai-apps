using Amazon.S3.Model;

namespace AccessReadAI.Infrastructure.Storage;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, Stream stream, string type, CancellationToken ct);
    Task<GetObjectResponse> OpenAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task PutBytesAsync(string key, byte[] bytes, string type, CancellationToken ct);
}

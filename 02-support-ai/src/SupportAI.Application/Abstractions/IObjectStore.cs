namespace SupportAI.Application.Abstractions;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
    Task<Stream> OpenRangeAsync(string key, long start, long end, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

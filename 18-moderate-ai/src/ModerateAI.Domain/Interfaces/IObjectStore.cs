namespace ModerateAI.Domain.Interfaces;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Task<bool> ReadyAsync(CancellationToken ct);
}

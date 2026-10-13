namespace TutorAI.Infrastructure.Abstractions;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

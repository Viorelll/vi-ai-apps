namespace DataAI.Application.Abstractions;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken cancellationToken);
    Task<string> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

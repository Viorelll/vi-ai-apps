namespace DocuAI.Application.Abstractions;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken cancellationToken);
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

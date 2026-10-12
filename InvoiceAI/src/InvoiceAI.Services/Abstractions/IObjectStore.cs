namespace InvoiceAI.Services.Abstractions;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

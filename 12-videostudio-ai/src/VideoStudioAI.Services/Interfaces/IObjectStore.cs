using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services.Interfaces;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task PutAsync(string key, string path, string contentType, CancellationToken ct);
    Task<StoredObject> DownloadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

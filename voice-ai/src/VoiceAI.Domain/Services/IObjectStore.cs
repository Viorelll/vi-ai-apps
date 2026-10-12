namespace VoiceAI.Domain.Services;

public interface IObjectStore
{
    Task EnsureBucketAsync(CancellationToken cancellationToken);
    Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken
    );
    Task<(byte[] Bytes, string ContentType)> GetAsync(
        string key,
        CancellationToken cancellationToken
    );
}

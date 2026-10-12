using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace IncidentAI.Infrastructure.Storage;

public interface IObjectStore
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<GetObjectResponse> GetAsync(string key, CancellationToken ct);
}

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration configuration) : IObjectStore
{
    private readonly string bucket = configuration["Storage:Bucket"] ?? "incidentai";
    private readonly SemaphoreSlim bucketLock = new(1, 1);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public async Task<GetObjectResponse> GetAsync(string key, CancellationToken ct)
    {
        await EnsureBucketAsync(ct);
        return await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        await bucketLock.WaitAsync(ct);
        try
        {
            try
            {
                await client.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucket }, ct);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
            }
        }
        finally { bucketLock.Release(); }
    }
}

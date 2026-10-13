using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace AccessReadAI.Infrastructure.Storage;

public sealed class ObjectStore(IAmazonS3 s3, IConfiguration config) : IObjectStore
{
    private readonly string bucket = config["Storage:Bucket"] ?? "accessreadai";

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(bucket, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await s3.PutBucketAsync(bucket, ct);
        }
    }

    public Task PutAsync(string key, Stream stream, string type, CancellationToken ct) =>
        s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = type,
                AutoCloseStream = false,
            },
            ct
        );

    public Task<GetObjectResponse> OpenAsync(string key, CancellationToken ct) =>
        s3.GetObjectAsync(bucket, key, ct);

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(bucket, key, ct);

    public async Task PutBytesAsync(string key, byte[] bytes, string type, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes);
        await PutAsync(key, stream, type, ct);
    }
}

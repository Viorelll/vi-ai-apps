using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ShopVisionAI.Infrastructure.Storage;

public sealed class ObjectStore(IAmazonS3 s3, IOptions<StorageOptions> options)
{
    private readonly string bucket = options.Value.Bucket;

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = bucket },
                ct
            );
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
        }
    }

    public Task PutAsync(string key, Stream input, string mime, CancellationToken ct) =>
        s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = input,
                ContentType = mime,
                AutoCloseStream = false,
            },
            ct
        );

    public Task<GetObjectResponse> OpenAsync(string key, CancellationToken ct) =>
        s3.GetObjectAsync(bucket, key, ct);

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(bucket, key, ct);
}

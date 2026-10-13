using Amazon.S3;
using Amazon.S3.Model;
using BuildBudgetAI.Domain.Interfaces;

namespace BuildBudgetAI.Infrastructure;

public sealed class S3ObjectStore(IAmazonS3 s3, StorageOptions options) : IObjectStore
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(options.Bucket, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = options.Bucket }, ct);
        }
    }

    public async Task PutAsync(string key, Stream data, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = options.Bucket,
                Key = key,
                InputStream = data,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            ct
        );
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        using var response = await s3.GetObjectAsync(options.Bucket, key, ct);
        using var output = new MemoryStream();
        await response.ResponseStream.CopyToAsync(output, ct);
        return output.ToArray();
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(options.Bucket, key, ct);
    }
}

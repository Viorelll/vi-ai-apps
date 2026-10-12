using Amazon.S3;
using Amazon.S3.Model;

namespace FeedbackAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, string bucket)
{
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var buckets = await client.ListBucketsAsync(ct);
                if (!buckets.Buckets.Any(x => x.BucketName == bucket)) await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
                return;
            }
            catch (Exception ex) when (attempt < 7 && ex is not OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), ct);
            }
        }
    }
    public Task PutAsync(string key, Stream body, string contentType, CancellationToken ct) => client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = body, ContentType = contentType, AutoCloseStream = false }, ct);
}

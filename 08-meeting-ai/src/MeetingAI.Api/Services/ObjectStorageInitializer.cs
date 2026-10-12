using Amazon.S3;
using Amazon.S3.Model;
namespace MeetingAI.Api.Services;

public static class ObjectStorageInitializer
{
    public static async Task EnsureBucketAsync(IAmazonS3 client, string bucket, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try { await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, cancellationToken); return; }
            catch (AmazonS3Exception error) when (error.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") { return; }
            catch (Exception) when (attempt < 9) { await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken); }
        }
        throw new InvalidOperationException("Object storage did not become ready after 10 attempts.");
    }
}

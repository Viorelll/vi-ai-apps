using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace VideoSearchAI.Infrastructure.Storage;

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

    public async Task PutAsync(
        string key,
        Stream stream,
        string contentType,
        CancellationToken ct
    ) =>
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                AutoCloseStream = false,
                ContentType = contentType,
            },
            ct
        );

    public Task<GetObjectResponse> OpenAsync(string key, CancellationToken ct) =>
        s3.GetObjectAsync(bucket, key, ct);

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await s3.DeleteObjectAsync(bucket, key, ct);

    public async Task PutTextAsync(string key, string text, CancellationToken ct)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        await PutAsync(key, stream, "application/json", ct);
    }

    public async Task<string> ReadTextAsync(string key, CancellationToken ct)
    {
        using var obj = await OpenAsync(key, ct);
        using var reader = new StreamReader(obj.ResponseStream);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task DeletePrefixAsync(string prefix, CancellationToken ct)
    {
        string? token = null;
        do
        {
            var page = await s3.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = bucket,
                    Prefix = prefix,
                    ContinuationToken = token,
                },
                ct
            );
            foreach (var item in page.S3Objects ?? [])
                await DeleteAsync(item.Key, ct);
            token = page.IsTruncated == true ? page.NextContinuationToken : null;
        } while (token != null);
    }
}

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using InvoiceAI.Services.Abstractions;
namespace InvoiceAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 s3, IOptions<StorageOptions> options) : IObjectStore
{
    private readonly string bucket = options.Value.Bucket;
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(new GetBucketLocationRequest { BucketName = bucket }, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound) { await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct); }
    }
    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = stream, ContentType = contentType, AutoCloseStream = false, UseChunkEncoding = false }, ct);
    }
    public async Task<Stream> OpenAsync(string key, CancellationToken ct) => new S3ResponseStream(await s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct));
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, ct);
    }
}

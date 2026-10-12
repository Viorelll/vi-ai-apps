using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using SupportAI.Application.Abstractions;
namespace SupportAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration config) : IObjectStore
{
    private readonly string bucket = config["Storage:Bucket"] ?? "supportai";
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        var response = await client.ListBucketsAsync(ct);
        if (response.Buckets?.Any(x => x.BucketName == bucket) != true)
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
    }
    public Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct) => client.PutObjectAsync(new PutObjectRequest
    {
        BucketName = bucket,
        Key = key,
        InputStream = stream,
        ContentType = contentType,
        AutoCloseStream = false
    }, ct);
    public async Task<Stream> OpenAsync(string key, CancellationToken ct) => new S3ResponseStream(await client.GetObjectAsync(bucket, key, ct));
    public async Task<Stream> OpenRangeAsync(string key, long start, long end, CancellationToken ct) => new S3ResponseStream(await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key, ByteRange = new ByteRange(start, end) }, ct));
    public Task DeleteAsync(string key, CancellationToken ct) => client.DeleteObjectAsync(bucket, key, ct);
}

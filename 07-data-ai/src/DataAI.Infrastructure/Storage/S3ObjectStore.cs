using Amazon.S3;
using Amazon.S3.Model;
using DataAI.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DataAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration configuration) : IObjectStore
{
    private string Bucket => configuration["Storage:Bucket"] ?? "dataai";
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        if ((await client.ListBucketsAsync(ct)).Buckets?.Any(x => x.BucketName == Bucket) != true)
            await client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket }, ct);
    }
    public async Task<string> PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        var result = await client.PutObjectAsync(new PutObjectRequest { BucketName = Bucket, Key = key, InputStream = content, ContentType = contentType, AutoCloseStream = false }, ct);
        return result.ETag ?? "";
    }
    public async Task DeleteAsync(string key, CancellationToken ct) => await client.DeleteObjectAsync(Bucket, key, ct);
}

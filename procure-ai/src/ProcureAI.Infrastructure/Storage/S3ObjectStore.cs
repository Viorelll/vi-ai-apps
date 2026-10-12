using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ProcureAI.Application.Abstractions;

namespace ProcureAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration configuration) : IObjectStore
{
    private string Bucket => configuration["Storage:Bucket"] ?? "procureai";
    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        if ((await client.ListBucketsAsync(ct)).Buckets?.Any(x => x.BucketName == Bucket) != true)
            await client.PutBucketAsync(new PutBucketRequest { BucketName = Bucket }, ct);
    }
    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct) =>
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    public async Task<Stream> OpenAsync(string key, CancellationToken ct) =>
        new S3ObjectResponseStream(await client.GetObjectAsync(Bucket, key, ct));
    public async Task DeleteAsync(string key, CancellationToken ct) => await client.DeleteObjectAsync(Bucket, key, ct);
}

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using VoiceAI.Domain.Services;

namespace VoiceAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration configuration) : IObjectStore
{
    private string Bucket => configuration["Storage:Bucket"] ?? "voiceai";

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var buckets = await client.ListBucketsAsync(cancellationToken);
        if (buckets.Buckets?.Any(x => x.BucketName == Bucket) != true)
            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = Bucket },
                cancellationToken
            );
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken
    )
    {
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken
        );
    }

    public async Task<(byte[] Bytes, string ContentType)> GetAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.GetObjectAsync(Bucket, key, cancellationToken);
        await using var content = new MemoryStream();
        await response.ResponseStream.CopyToAsync(content, cancellationToken);
        return (content.ToArray(), response.Headers.ContentType ?? "application/octet-stream");
    }
}

using Amazon.S3;
using Amazon.S3.Model;
using EvalAI.Infrastructure.Abstractions;
using Microsoft.Extensions.Configuration;

namespace EvalAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration configuration) : IObjectStore
{
    private string Bucket => configuration["Storage:Bucket"] ?? "evalai";

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var response = await client.ListBucketsAsync(cancellationToken);
        if (response.Buckets?.Any(bucket => bucket.BucketName == Bucket) != true)
        {
            await client.PutBucketAsync(
                new PutBucketRequest { BucketName = Bucket },
                cancellationToken
            );
        }
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

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var response = await client.GetObjectAsync(Bucket, key, cancellationToken);
        return new S3ObjectResponseStream(response);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        client.DeleteObjectAsync(Bucket, key, cancellationToken);
}

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using ModerateAI.Domain.Interfaces;

namespace ModerateAI.Infrastructure;

public sealed class S3ObjectStore(IAmazonS3 client, IConfiguration config) : IObjectStore
{
    private readonly string bucket = config["Storage:Bucket"] ?? "moderateai";

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (!await ReadyAsync(ct))
        {
            try
            {
                await client.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
            }
            catch (AmazonS3Exception e)
                when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") { }
        }
    }

    public async Task<bool> ReadyAsync(CancellationToken ct)
    {
        try
        {
            await client.GetBucketLocationAsync(
                new GetBucketLocationRequest { BucketName = bucket },
                ct
            );
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task PutAsync(string key, Stream stream, string contentType, CancellationToken ct)
    {
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            ct
        );
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        using var response = await client.GetObjectAsync(bucket, key, ct);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await client.DeleteObjectAsync(bucket, key, ct);
    }
}

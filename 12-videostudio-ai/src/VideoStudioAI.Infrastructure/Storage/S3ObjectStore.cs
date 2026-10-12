using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Infrastructure.Storage;

public sealed class S3ObjectStore(IAmazonS3 s3, IConfiguration config) : IObjectStore
{
    private readonly string bucket = config["Storage:Bucket"] ?? "videostudio";

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(bucket, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
        }
    }

    public async Task PutAsync(string key, string path, string contentType, CancellationToken ct)
    {
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                FilePath = path,
                ContentType = contentType,
            },
            ct
        );
    }

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await s3.DeleteObjectAsync(bucket, key, ct);

    public async Task<StoredObject> DownloadAsync(string key, CancellationToken ct)
    {
        using var response = await s3.GetObjectAsync(bucket, key, ct);
        return await TemporaryFiles.SpoolAsync(
            response.ResponseStream,
            response.Headers.ContentType,
            ct
        );
    }
}

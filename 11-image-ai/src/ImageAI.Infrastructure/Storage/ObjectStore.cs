using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ImageAI.Infrastructure.Storage;

public sealed class ObjectStore(IAmazonS3 s3, IOptions<StorageOptions> options)
{
    private readonly string bucket = options.Value.Bucket;

    public async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            await s3.GetBucketLocationAsync(bucket, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await s3.PutBucketAsync(bucket, ct);
        }
    }

    public async Task PutAsync(string key, byte[] bytes, string mime, CancellationToken ct)
    {
        using var input = new MemoryStream(bytes);
        await s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = input,
                ContentType = mime,
                AutoCloseStream = false,
            },
            ct
        );
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken ct)
    {
        using var response = await s3.GetObjectAsync(bucket, key, ct);
        if (response.ContentLength > Validation.ImageValidator.MaxBytes)
            throw new InvalidOperationException("object_size_limit");
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int count;
        while ((count = await response.ResponseStream.ReadAsync(buffer, ct)) > 0)
        {
            if (output.Length + count > Validation.ImageValidator.MaxBytes)
                throw new InvalidOperationException("object_size_limit");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        return output.ToArray();
    }

    public Task DeleteAsync(string key, CancellationToken ct) =>
        s3.DeleteObjectAsync(bucket, key, ct);
}

using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using VideoStudioAI.Ai.Providers;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Infrastructure.Media;
using VideoStudioAI.Infrastructure.Storage;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Tests;

public sealed class MediaAndStorageTests
{
    [Fact]
    public async Task RealMp4ValidatesAndWrongDimensionsFail()
    {
        var demo = new DemoProvider();
        var job = await demo.SubmitAsync(new("fixture", 4, "1280x720", "demo"), default);
        var path = await demo.DownloadAsync(job.Id, default);
        try
        {
            await new MediaValidator().ValidateAsync(path, 4, 1280, 720, default);
            await Assert.ThrowsAsync<AppException>(() =>
                new MediaValidator().ValidateAsync(path, 4, 720, 1280, default)
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task GarbageContentCannotBecomeReady()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "not a video");
            await Assert.ThrowsAsync<AppException>(() =>
                new MediaValidator().ValidateAsync(path, 8, 1280, 720, default)
            );
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task S3RoundTripPreservesHashAndDeleteRemovesObject()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { { "Storage:Bucket", "videostudio-tests" } }
            )
            .Build();
        using var client = new AmazonS3Client(
            new BasicAWSCredentials("dummy-minio", "dummy-minio-password"),
            new AmazonS3Config
            {
                ServiceURL =
                    Environment.GetEnvironmentVariable("TEST_S3") ?? "http://127.0.0.1:59000",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        );
        var store = new S3ObjectStore(client, config);
        await store.EnsureBucketAsync(default);
        var path = Path.GetTempFileName();
        var key = $"tests/{Guid.NewGuid()}";
        try
        {
            var bytes = RandomNumberGenerator.GetBytes(1024);
            await File.WriteAllBytesAsync(path, bytes);
            await store.PutAsync(key, path, "application/octet-stream", default);
            await using var obj = await store.DownloadAsync(key, default);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(obj.Path));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), obj.Sha256);
            await store.DeleteAsync(key, default);
            await Assert.ThrowsAsync<Amazon.S3.Model.NoSuchKeyException>(() =>
                store.DownloadAsync(key, default)
            );
        }
        finally
        {
            File.Delete(path);
            await store.DeleteAsync(key, default);
        }
    }
}

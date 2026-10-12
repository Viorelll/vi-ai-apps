using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Database.Persistence;
using ProcureAI.Infrastructure.Storage;

namespace ProcureAI.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class S3StorageTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RealS3UploadHashDeduplicationDownloadAndCleanup()
    {
        var endpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT")
            ?? throw new InvalidOperationException("Set TEST_S3_ENDPOINT for required real S3 integration tests.");
        var user = Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY") ?? throw new InvalidOperationException("TEST_S3_ACCESS_KEY required");
        var password = Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY") ?? throw new InvalidOperationException("TEST_S3_SECRET_KEY required");
        using var client = new AmazonS3Client(new BasicAWSCredentials(user, password), new AmazonS3Config
        { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        var bucket = "procureai-test-" + Guid.NewGuid().ToString("N");
        var objects = new S3ObjectStore(client, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Storage:Bucket"] = bucket }).Build());
        var repository = new ProcurementRepository(fixture.Factory);
        var service = new DocumentService(repository, objects);
        var bytes = System.Text.Encoding.UTF8.GetBytes("Real procurement offer: EUR 1000 + shipping 50.");
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "offer.txt");
        var uploaded = await service.UploadAsync(file, default);
        var duplicate = await service.UploadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "same.txt"), default);
        Assert.Equal(uploaded.Document.Id, duplicate.Document.Id); Assert.True(duplicate.Existing);
        await using (var stream = await objects.OpenAsync(uploaded.Document.ObjectKey, default))
        {
            using var reader = new StreamReader(stream); Assert.Equal(System.Text.Encoding.UTF8.GetString(bytes), await reader.ReadToEndAsync());
        }
        var forgedPdf = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "fake.pdf");
        Assert.Equal("file_signature_mismatch", (await Assert.ThrowsAsync<AppException>(() => service.UploadAsync(forgedPdf, default))).Code);
        await repository.DeleteDocumentAsync(uploaded.Document.Id, default);
        await objects.DeleteAsync(uploaded.Document.ObjectKey, default);
        var replacement = await service.UploadAsync(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "replacement.txt"), default);
        Assert.NotEqual(uploaded.Document.Id, replacement.Document.Id);
        Assert.False(replacement.Existing);
        await objects.DeleteAsync(replacement.Document.ObjectKey, default);
        Assert.Empty((await client.ListObjectsV2Async(new() { BucketName = bucket })).S3Objects ?? []);
        await client.DeleteBucketAsync(bucket);
    }
}

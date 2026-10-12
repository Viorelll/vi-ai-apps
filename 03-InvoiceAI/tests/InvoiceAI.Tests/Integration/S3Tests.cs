using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using InvoiceAI.Database.Repositories;
using InvoiceAI.Infrastructure.Storage;
using InvoiceAI.Services.Services;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class S3Tests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task UploadHashDedupContentAndDelete()
    {
        var o = new StorageOptions { Endpoint = Environment.GetEnvironmentVariable("INVOICEAI_TEST_S3") ?? "http://localhost:9000", Bucket = "invoiceai-tests-" + Guid.NewGuid().ToString("N"), AccessKey = Environment.GetEnvironmentVariable("INVOICEAI_TEST_S3_ACCESS_KEY") ?? "invoiceai-storage", SecretKey = Environment.GetEnvironmentVariable("INVOICEAI_TEST_S3_SECRET_KEY") ?? "dummy-storage-password-change-me" };
        using var client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), new AmazonS3Config { ServiceURL = o.Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        var store = new S3ObjectStore(client, Options.Create(o));
        var repo = new DocumentRepository(fixture.Factory);
        var service = new DocumentService(repo, store);
        var bytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\nseed " + Guid.NewGuid());
        try
        {
            var upload = await service.UploadAsync(new MemoryStream(bytes), "seed.pdf", "application/pdf", default);
            var dup = await service.UploadAsync(new MemoryStream(bytes), "again.pdf", "application/pdf", default);
            Assert.True(dup.Duplicate);
            Assert.Equal(upload.DocumentId, dup.DocumentId);
            var doc = await repo.GetAsync(upload.DocumentId, default);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), doc.Sha256);
            await using var content = await store.OpenAsync(doc.ObjectKey, default);
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
            await store.DeleteAsync(doc.ObjectKey, default);
            await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => store.OpenAsync(doc.ObjectKey, default));
        }
        finally { await client.DeleteBucketAsync(o.Bucket); }
    }
}

using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShopVisionAI.Database;
using ShopVisionAI.Infrastructure.Storage;

namespace ShopVisionAI.Tests.Integration;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public DbContextOptions<AppDbContext> Options { get; } =
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                Environment.GetEnvironmentVariable("SHOPVISION_TEST_DB")
                    ?? "Host=localhost;Port=15432;Database=shopvisionai_tests;Username=shopvisionai;Password=dummy-local-database-password"
            )
            .Options;
    private readonly IAmazonS3 s3 = new AmazonS3Client(
        new BasicAWSCredentials("shopvision-local", "dummy-local-storage-password"),
        new AmazonS3Config
        {
            ServiceURL =
                Environment.GetEnvironmentVariable("SHOPVISION_TEST_S3")
                ?? "http://localhost:19000",
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1",
        }
    );
    public ObjectStore Store { get; }

    public DatabaseFixture() =>
        Store = new ObjectStore(
            s3,
            Microsoft.Extensions.Options.Options.Create(
                new StorageOptions { Bucket = "shopvision-tests" }
            )
        );

    public async Task InitializeAsync()
    {
        await using var db = Create();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await Store.EnsureBucketAsync(CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        s3.Dispose();
        return Task.CompletedTask;
    }

    public AppDbContext Create() => new(Options);
}

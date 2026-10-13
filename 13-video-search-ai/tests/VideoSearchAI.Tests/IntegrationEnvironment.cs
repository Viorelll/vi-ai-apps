using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using VideoSearchAI.Database;
using VideoSearchAI.Infrastructure.Storage;

namespace VideoSearchAI.Tests;

public sealed class IntegrationEnvironment : IDisposable
{
    private readonly string database = "test_" + Guid.NewGuid().ToString("N");
    private readonly string root =
        Environment.GetEnvironmentVariable("VIDEOSEARCH_TEST_DB")
        ?? "Host=localhost;Port=55413;Database=videosearchai;Username=videosearchai;Password=demo-db-password";
    private ServiceProvider? provider;
    private readonly SemaphoreSlim initializeLock = new(1, 1);
    private bool initialized;
    public IDbContextFactory<AppDbContext> Factory { get; private set; } = null!;
    public ObjectStore Store { get; private set; } = null!;
    public IAmazonS3 S3 { get; private set; } = null!;
    public string Bucket { get; } = "test-" + Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        await initializeLock.WaitAsync();
        try
        {
            if (initialized)
                return;
            var admin = new NpgsqlConnectionStringBuilder(root)
            {
                Timeout = 90,
                CommandTimeout = 90,
            };
            await using var connection = new NpgsqlConnection(admin.ConnectionString);
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await cmd.ExecuteNonQueryAsync();
            var cb = new NpgsqlConnectionStringBuilder(root)
            {
                Database = database,
                Timeout = 90,
                CommandTimeout = 90,
            };
            var services = new ServiceCollection();
            services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(cb.ToString()));
            provider = services.BuildServiceProvider();
            Factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var db = await Factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            var storage = new StorageOptions
            {
                Endpoint =
                    Environment.GetEnvironmentVariable("VIDEOSEARCH_TEST_S3")
                    ?? "http://localhost:59013",
                AccessKey = "demo-minio",
                SecretKey = "demo-minio-password",
                Bucket = Bucket,
            };
            S3 = new AmazonS3Client(
                new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = storage.Endpoint,
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-1",
                }
            );
            Store = new ObjectStore(S3, Options.Create(storage));
            await Store.EnsureBucketAsync(default);
            initialized = true;
        }
        finally
        {
            initializeLock.Release();
        }
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async ValueTask DisposeAsync()
    {
        if (initialized && S3 != null)
        {
            await Store.DeletePrefixAsync("", default);
            await S3.DeleteBucketAsync(Bucket);
            S3.Dispose();
        }
        if (provider != null)
            await provider.DisposeAsync();
        // Compose's test overlay gives PostgreSQL an isolated test-only volume, which can be
        // removed after the suite without touching the application's development database.
        NpgsqlConnection.ClearAllPools();
    }
}

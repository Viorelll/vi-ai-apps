using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SupportAI.Application.Abstractions;
using SupportAI.Infrastructure.Storage;
using Xunit;
namespace SupportAI.IntegrationTests;

public sealed class LaboratoryFixture : IAsyncLifetime
{
    private readonly string database = "supportai_tests_" + Guid.NewGuid().ToString("N");
    private string admin = "";
    private AmazonS3Client? s3;
    private bool bucketCreated;
    public TestDbFactory Factory { get; private set; } = null!;
    public IObjectStore Storage { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION") ?? throw new InvalidOperationException("Set TEST_DATABASE_CONNECTION to a real PostgreSQL admin connection; integration tests are never silently skipped.");
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(admin) { Database = database };
        Factory = new(builder.ConnectionString);
        await using var db = Factory.CreateDbContext();
        await db.Database.MigrateAsync();
        var endpoint = Environment.GetEnvironmentVariable("TEST_S3_ENDPOINT") ?? throw new InvalidOperationException("Set TEST_S3_ENDPOINT to a real MinIO/S3 endpoint.");
        s3 = new(new BasicAWSCredentials(Environment.GetEnvironmentVariable("TEST_S3_ACCESS_KEY"), Environment.GetEnvironmentVariable("TEST_S3_SECRET_KEY")), new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Storage:Bucket", database.Replace('_', '-') } }).Build();
        Storage = new S3ObjectStore(s3, config);
        await Storage.EnsureBucketAsync(default);
        bucketCreated = true;
    }
    public async Task DisposeAsync()
    {
        if (s3 is not null && bucketCreated)
        {
            var bucket = database.Replace('_', '-');
            var objects = await s3.ListObjectsV2Async(new()
            {
                BucketName = bucket
            });
            foreach (var obj in objects.S3Objects ?? [])
                await s3.DeleteObjectAsync(bucket, obj.Key);
            await s3.DeleteBucketAsync(bucket);
            s3.Dispose();
        }
        if (admin.Length == 0)
            return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}

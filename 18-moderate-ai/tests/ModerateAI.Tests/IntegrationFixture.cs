using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModerateAI.Infrastructure;
using Npgsql;
using Xunit;

namespace ModerateAI.Tests;

public sealed class IntegrationFixture : IAsyncLifetime
{
    private readonly string database = "moderateai_test_" + Guid.NewGuid().ToString("N");
    private readonly string bucket = "moderateai-test-" + Guid.NewGuid().ToString("N");
    private string admin = "";
    private AmazonS3Client client = null!;
    public TestDbFactory Factory { get; private set; } = null!;
    public S3ObjectStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        admin =
            Environment.GetEnvironmentVariable("MODERATEAI_TEST_DB")
            ?? throw new InvalidOperationException(
                "Run scripts/test-integration.py with real PostgreSQL and MinIO; no implicit skips."
            );
        var builder = new NpgsqlConnectionStringBuilder(admin);
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
            await command.ExecuteNonQueryAsync();
        }
        builder.Database = database;
        Factory = new(builder.ConnectionString);
        await using (var db = Factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync();
        }
        string access =
                Environment.GetEnvironmentVariable("MODERATEAI_TEST_S3_KEY")
                ?? throw new InvalidOperationException("S3 key required"),
            secret =
                Environment.GetEnvironmentVariable("MODERATEAI_TEST_S3_SECRET")
                ?? throw new InvalidOperationException("S3 secret required");
        client = new(
            new BasicAWSCredentials(access, secret),
            new AmazonS3Config
            {
                ServiceURL = "http://127.0.0.1:9088",
                ForcePathStyle = true,
                AuthenticationRegion = "us-east-1",
            }
        );
        Store = new(
            client,
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { { "Storage:Bucket", bucket } }
                )
                .Build()
        );
        await Store.EnsureBucketAsync(default);
    }

    public async Task DisposeAsync()
    {
        if (client != null)
        {
            var list = await client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = bucket }
            );
            foreach (var obj in list.S3Objects ?? [])
                await client.DeleteObjectAsync(bucket, obj.Key);
            await client.DeleteBucketAsync(bucket);
            client.Dispose();
        }
        if (admin.Length > 0)
        {
            NpgsqlConnection.ClearAllPools();
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS {database} WITH (FORCE)",
                connection
            );
            await command.ExecuteNonQueryAsync();
        }
    }
}

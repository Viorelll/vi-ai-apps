using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using EvalAI.Database.Jobs;
using EvalAI.Database.Persistence;
using EvalAI.Infrastructure.Ai;
using EvalAI.Infrastructure.Extraction;
using EvalAI.Infrastructure.Storage;
using EvalAI.Services.Documents;
using EvalAI.Services.Evaluations;
using EvalAI.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
namespace EvalAI.Tests;

public sealed class TestRig : IAsyncDisposable
{
    private readonly string name = "evalai_test_" + Guid.NewGuid().ToString("N");
    private readonly string bucket = "evalai-test-" + Guid.NewGuid().ToString("N");
    private readonly string admin = Environment.GetEnvironmentVariable("EVALAI_TEST_DB") ?? "Host=localhost;Port=55420;Database=evalai;Username=evalai;Password=replace-local-db-password";
    public TestDbFactory Factory { get; private set; } = null!; public LocalAiClient Ai { get; private set; } = null!;
    public DatasetService Datasets { get; private set; } = null!; public EvaluationService Evaluations { get; private set; } = null!; public DocumentService Documents { get; private set; } = null!;
    public JobStore Jobs { get; private set; } = null!; public S3ObjectStore Storage { get; private set; } = null!;
    public DurableWorker? Worker { get; private set; }
    private AmazonS3Client s3 = null!;
    public async Task<TestRig> InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync(); await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", connection); await cmd.ExecuteNonQueryAsync();
        var cs = new NpgsqlConnectionStringBuilder(admin) { Database = name }; Factory = new(cs.ConnectionString);
        await using var db = Factory.CreateDbContext(); await db.Database.MigrateAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Foundry:Mode"] = "Demo", ["Storage:Bucket"] = bucket }).Build();
        s3 = new(new BasicAWSCredentials("evalai-storage", "replace-local-storage-password"), new AmazonS3Config { ServiceURL = Environment.GetEnvironmentVariable("EVALAI_TEST_S3") ?? "http://localhost:59020", ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        Storage = new(s3, config); await Storage.EnsureBucketAsync(default);
        Ai = new(new(new HttpClient(), config), new(new HttpClient(), Storage, config)); Datasets = new(Factory); Evaluations = new(Factory, new(config)); Documents = new(Factory, Storage); Jobs = new(Factory); return this;
    }
    public async Task StartWorkerAsync() { Worker = new(Jobs, Factory, Ai, Storage, NullLogger<DurableWorker>.Instance); await Worker.StartAsync(default); }
    public async Task WaitRunAsync(Guid id) { for (int i = 0; i < 150; i++) { await using var db = Factory.CreateDbContext(); var r = await db.EvalRuns.SingleAsync(r => r.Id == id); if (r.State is "Completed" or "Failed" or "Partial" or "Canceled") return; await Task.Delay(100); } throw new TimeoutException("Evaluation did not finish."); }
    public async ValueTask DisposeAsync()
    {
        if (Worker != null) { await Worker.StopAsync(default); Worker.Dispose(); }
        if (s3 != null) { var objects = await s3.ListObjectsV2Async(new() { BucketName = bucket }); foreach (var item in objects.S3Objects ?? []) await s3.DeleteObjectAsync(bucket, item.Key); await s3.DeleteBucketAsync(bucket); s3.Dispose(); }
        NpgsqlConnection.ClearAllPools(); await using var connection = new NpgsqlConnection(admin); await connection.OpenAsync(); await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", connection); await cmd.ExecuteNonQueryAsync();
    }
}

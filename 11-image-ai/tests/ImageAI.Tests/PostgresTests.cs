using ImageAI.Database;
using ImageAI.Domain.Entities;
using ImageAI.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ImageAI.Tests;

// Run explicitly against a disposable PostgreSQL DB, never EF InMemory.
[Trait("Category", "Integration")]
public sealed class PostgresTests : IAsyncLifetime
{
    private string connection = "";
    private readonly string database = "imageai_test_" + Guid.NewGuid().ToString("N");
    private string Admin =>
        Environment.GetEnvironmentVariable("IMAGEAI_TEST_DB")
        ?? throw new InvalidOperationException(
            "Set IMAGEAI_TEST_DB for real PostgreSQL integration tests."
        );

    private AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    public async Task InitializeAsync()
    {
        await using var c = new NpgsqlConnection(Admin);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {database}", c);
        await cmd.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(Admin) { Database = database };
        connection = builder.ConnectionString;
        await using var db = Create();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var c = new NpgsqlConnection(Admin);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", c);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<GenerationJob> SeedAsync()
    {
        await using var db = Create();
        var p = new ImageProject { Brief = "Test product image" };
        var r = new ImageRun { ProjectId = p.Id, RequestId = Guid.NewGuid() };
        var j = new GenerationJob { RunId = r.Id };
        db.AddRange(p, r, j);
        await db.SaveChangesAsync();
        return j;
    }

    [Fact]
    public async Task ClaimsEachJobOnlyOnceWithConcurrentWorkers()
    {
        var job = await SeedAsync();
        await using var a = Create();
        await using var b = Create();
        var claims = await Task.WhenAll(
            new JobLeaser(a).ClaimAsync(default),
            new JobLeaser(b).ClaimAsync(default)
        );
        Assert.Single(claims, x => x != null);
        Assert.Equal(job.Id, claims.Single(x => x != null)!.Id);
    }

    [Fact]
    public async Task ExpiredLeaseBecomesUncertainWithoutProviderReplay()
    {
        var job = await SeedAsync();
        await using var db = Create();
        await new JobLeaser(db).ClaimAsync(default);
        await db
            .Jobs.Where(x => x.Id == job.Id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddMinutes(-1))
            );
        await new JobLeaser(db).RecoverExpiredAsync(default);
        db.ChangeTracker.Clear();
        Assert.Equal("Uncertain", (await db.Jobs.SingleAsync()).State);
        Assert.Null(await new JobLeaser(db).ClaimAsync(default));
    }

    [Fact]
    public async Task RevisionProtectsConcurrentMutations()
    {
        await SeedAsync();
        await using var a = Create();
        await using var b = Create();
        var x = await a.Projects.SingleAsync();
        var y = await b.Projects.SingleAsync();
        x.Revision++;
        await a.SaveChangesAsync();
        y.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task UniqueRequestIdsPreventDuplicateRuns()
    {
        await SeedAsync();
        await using var db = Create();
        var prior = await db.Runs.SingleAsync();
        db.Runs.Add(new ImageRun { ProjectId = prior.ProjectId, RequestId = prior.RequestId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task MigrationIsRepeatableAndSchemaMatchesModel()
    {
        await using var db = Create();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

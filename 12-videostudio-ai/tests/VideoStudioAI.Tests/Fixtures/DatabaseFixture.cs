using Microsoft.EntityFrameworkCore;
using Npgsql;
using VideoStudioAI.Database;

namespace VideoStudioAI.Tests.Fixtures;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = "";
    private readonly string name = "videostudio_test_" + Guid.NewGuid().ToString("N");
    private string Admin =>
        Environment.GetEnvironmentVariable("TEST_POSTGRES")
        ?? "Host=127.0.0.1;Port=55432;Database=videostudio;Username=videostudio;Password=dummy-db-password";

    public AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(Admin);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
        await cmd.ExecuteNonQueryAsync();
        ConnectionString = new NpgsqlConnectionStringBuilder(Admin)
        {
            Database = name,
        }.ConnectionString;
        await using var db = Create();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(Admin);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("Postgres", DisableParallelization = true)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using ProcureAI.Database.Persistence;

namespace ProcureAI.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    public IDbContextFactory<AppDbContext> Factory { get; private set; } = null!;
    private readonly string database = "procureai_test_" + Guid.NewGuid().ToString("N");
    private string adminConnection = "";
    public async Task InitializeAsync()
    {
        var connection = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? throw new InvalidOperationException("Set TEST_DB_CONNECTION to run required real PostgreSQL integration tests.");
        var admin = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres" };
        adminConnection = admin.ConnectionString;
        await using var sql = new NpgsqlConnection(adminConnection); await sql.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", sql); await create.ExecuteNonQueryAsync();
        var config = new NpgsqlConnectionStringBuilder(connection) { Database = database };
        Factory = new PooledDbContextFactory<AppDbContext>(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(config.ConnectionString).Options);
        await using var db = await Factory.CreateDbContextAsync(); await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var sql = new NpgsqlConnection(adminConnection); await sql.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", sql); await drop.ExecuteNonQueryAsync();
    }
}

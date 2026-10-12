using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using InvoiceAI.Database.Persistence;
namespace InvoiceAI.Tests.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string name = "invoiceai_tests_" + Guid.NewGuid().ToString("N");
    private readonly string master = Environment.GetEnvironmentVariable("INVOICEAI_TEST_DB") ?? "Host=localhost;Database=invoiceai;Username=invoiceai;Password=dummy-db-password-change-me";
    public IDbContextFactory<AppDbContext> Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(master);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await cmd.ExecuteNonQueryAsync();
        var cs = new NpgsqlConnectionStringBuilder(master) { Database = name }.ConnectionString;
        Factory = new PooledDbContextFactory<AppDbContext>(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
        await using var db = await Factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(master);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
        await cmd.ExecuteNonQueryAsync();
    }
}

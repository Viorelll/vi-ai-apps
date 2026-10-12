using Npgsql;

namespace ContractAI.Tests.Integration;

public sealed class TestDatabase(
    string administrationConnection,
    string name,
    string connectionString
) : IAsyncDisposable
{
    public string ConnectionString { get; } = connectionString;

    public static async Task<TestDatabase> CreateAsync()
    {
        var original =
            Environment.GetEnvironmentVariable("CONTRACTAI_TEST_DB")
            ?? throw new InvalidOperationException("A test PostgreSQL connection is required.");
        var name = "contractai_lease_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(original);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        var isolated = new NpgsqlConnectionStringBuilder(original) { Database = name };
        return new TestDatabase(original, name, isolated.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(administrationConnection);
        await connection.OpenAsync();
        // Only the random database created above is removed; shared test data is preserved.
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE \"{name}\" WITH (FORCE)",
            connection
        );
        await command.ExecuteNonQueryAsync();
    }
}

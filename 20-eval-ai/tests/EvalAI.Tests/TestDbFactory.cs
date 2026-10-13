using EvalAI.Database.Persistence;
using Microsoft.EntityFrameworkCore;
namespace EvalAI.Tests;

public sealed class TestDbFactory(string connection) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    public Task<AppDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
}

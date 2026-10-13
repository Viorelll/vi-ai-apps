using Microsoft.EntityFrameworkCore;
using ModerateAI.Database;

namespace ModerateAI.Tests;

public sealed class TestDbFactory(string connection) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
}

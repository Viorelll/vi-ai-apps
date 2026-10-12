using Microsoft.EntityFrameworkCore;
using SupportAI.Database.Persistence;
namespace SupportAI.IntegrationTests;

public sealed class TestDbFactory(string connection) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
}

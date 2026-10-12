using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAI.Database.Persistence;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__App")
            ?? "Host=127.0.0.1;Port=5432;Database=dataai;Username=dataai;Password=design-time-only";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        return new AppDbContext(options);
    }
}

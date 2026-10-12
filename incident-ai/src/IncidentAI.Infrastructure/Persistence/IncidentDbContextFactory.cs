using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IncidentAI.Infrastructure.Persistence;

public sealed class IncidentDbContextFactory : IDesignTimeDbContextFactory<IncidentDbContext>
{
    public IncidentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__App")
            ?? "Host=localhost;Database=incidentai;Username=incidentai;Password=incidentai";
        var options = new DbContextOptionsBuilder<IncidentDbContext>().UseNpgsql(connection).Options;
        return new IncidentDbContext(options);
    }
}

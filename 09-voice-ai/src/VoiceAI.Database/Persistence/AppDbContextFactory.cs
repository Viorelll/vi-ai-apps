using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VoiceAI.Database.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                Environment.GetEnvironmentVariable("ConnectionStrings__App")
                    ?? "Host=localhost;Database=voiceai;Username=voiceai;Password=voiceai"
            )
            .Options;
        return new AppDbContext(options);
    }
}

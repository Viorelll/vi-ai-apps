using Microsoft.Extensions.DependencyInjection;
using MeetingAI.Database;
using Microsoft.EntityFrameworkCore;
namespace MeetingAI.Infrastructure.Persistence;

public static class SchemaInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingDbContext>();
        await db.Database.MigrateAsync(ct);
    }
}

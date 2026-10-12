using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace MeetingAI.Database;

public sealed class MeetingDbContextFactory : IDesignTimeDbContextFactory<MeetingDbContext>
{
    public MeetingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MeetingDbContext>()
            .UseNpgsql("Host=localhost;Database=meetingai;Username=meetingai;Password=design-time-only")
            .Options;
        return new MeetingDbContext(options);
    }
}

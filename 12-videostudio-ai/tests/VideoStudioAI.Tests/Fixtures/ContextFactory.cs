using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Database;

namespace VideoStudioAI.Tests.Fixtures;

public sealed class ContextFactory(DatabaseFixture fixture) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => fixture.Create();
}

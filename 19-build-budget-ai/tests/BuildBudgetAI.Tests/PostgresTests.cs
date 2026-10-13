using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BuildBudgetAI.Tests;

public sealed class PostgresTests
{
    [Fact]
    public async Task RealDatabaseMigratesAndEnforcesConcurrencyAndUniqueHash()
    {
        string connection =
            Environment.GetEnvironmentVariable("BUILDBUDGET_TEST_DB")
            ?? throw new InvalidOperationException(
                "BUILDBUDGET_TEST_DB must point to a dedicated PostgreSQL test database. No InMemory substitute."
            );
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        var p = await new PlanService(db).CreateAsync(
            new("Integration " + Guid.NewGuid(), "EUR"),
            default
        );
        await using var other = new AppDbContext(options);
        var stale = await other.Plans.SingleAsync(x => x.Id == p.Id);
        p.Revision++;
        await db.SaveChangesAsync();
        stale.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        var hash = Guid.NewGuid().ToString("N");
        db.Documents.Add(new() { Sha256 = hash });
        await db.SaveChangesAsync();
        db.Documents.Add(new() { Sha256 = hash });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

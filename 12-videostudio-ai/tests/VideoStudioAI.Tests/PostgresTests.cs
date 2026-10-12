using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Services;
using VideoStudioAI.Tests.Fixtures;

namespace VideoStudioAI.Tests;

[Collection("Postgres")]
public sealed class PostgresTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task MigrationsAreRepeatableAndAllTablesExist()
    {
        await using var db = fixture.Create();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Equal(11, db.Model.GetEntityTypes().Count());
    }

    [Fact]
    public async Task UniqueRequestIdsPreventDuplicateExternalJobs()
    {
        await using var db = fixture.Create();
        var p = new VideoProject { Brief = "test" };
        db.Add(p);
        var request = Guid.NewGuid();
        db.Add(new GenerationJob { ProjectId = p.Id, RequestId = request });
        await db.SaveChangesAsync();
        db.Add(new GenerationJob { ProjectId = p.Id, RequestId = request });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task RevisionDetectsConcurrentEdits()
    {
        await using var a = fixture.Create();
        var p = new VideoProject { Brief = "concurrency" };
        a.Add(p);
        await a.SaveChangesAsync();
        await using var b = fixture.Create();
        var second = await b.Projects.SingleAsync(x => x.Id == p.Id);
        p.Revision++;
        await a.SaveChangesAsync();
        second.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task AtomicClaimsAreExclusiveAndExpiredLeaseIsReclaimed()
    {
        await using var db = fixture.Create();
        await db.Jobs.ExecuteDeleteAsync();
        var p = new VideoProject { Brief = "lease" };
        db.Add(p);
        var j = new GenerationJob { ProjectId = p.Id, RequestId = Guid.NewGuid() };
        db.Add(j);
        await db.SaveChangesAsync();
        var lease = new JobLeaseService(new ContextFactory(fixture));
        var results = await Task.WhenAll(
            lease.ClaimVideoAsync(default),
            lease.ClaimVideoAsync(default)
        );
        var winner = Assert.Single(results, x => x is not null);
        Assert.Equal(j.Id, winner!.Id);
        await db
            .Jobs.Where(x => x.Id == j.Id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1))
            );
        var reclaimed = await lease.ClaimVideoAsync(default);
        Assert.NotNull(reclaimed);
        Assert.NotEqual(winner.LeaseToken, reclaimed.LeaseToken);
    }

    [Fact]
    public async Task StaleLeaseCannotCompleteClaimedJob()
    {
        await using var db = fixture.Create();
        await db.Jobs.ExecuteDeleteAsync();
        var p = new VideoProject { Brief = "stale" };
        db.Add(p);
        var j = new GenerationJob { ProjectId = p.Id, RequestId = Guid.NewGuid() };
        db.Add(j);
        await db.SaveChangesAsync();
        var lease = new JobLeaseService(new ContextFactory(fixture));
        await lease.ClaimVideoAsync(default);
        await using var old = fixture.Create();
        var stale = await old.Jobs.SingleAsync(x => x.Id == j.Id);
        await db
            .Jobs.Where(x => x.Id == j.Id)
            .ExecuteUpdateAsync(s =>
                s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1))
            );
        await lease.ClaimVideoAsync(default);
        stale.State = "Ready";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => old.SaveChangesAsync());
    }
}

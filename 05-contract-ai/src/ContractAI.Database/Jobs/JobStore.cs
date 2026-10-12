using ContractAI.Database.Persistence;
using ContractAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContractAI.Database.Jobs;

public sealed class JobStore(IDbContextFactory<AppDbContext> factory)
{
    public async Task<IngestionJob?> ClaimAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        var jobs = await db
            .IngestionJobs.FromSqlInterpolated(
                $"""
                UPDATE app."IngestionJobs" SET "Status" = 'Running', "LeaseToken" = {token}, "LeaseUntilUtc" = now() + interval '90 seconds'
                WHERE "Id" = (SELECT "Id" FROM app."IngestionJobs"
                  WHERE ("Status" IN ('Queued', 'RetryWaiting') AND "NextAttemptAtUtc" <= now())
                   OR ("Status" = 'Running' AND "LeaseUntilUtc" < now())
                  ORDER BY "NextAttemptAtUtc", "Id" FOR UPDATE SKIP LOCKED LIMIT 1)
                RETURNING *
                """
            )
            .AsNoTracking()
            .ToListAsync(ct);
        return jobs.SingleOrDefault();
    }

    public async Task<bool> RenewAsync(IngestionJob job, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db
                .IngestionJobs.Where(x =>
                    x.Id == job.Id && x.LeaseToken == job.LeaseToken && x.Status == "Running"
                )
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddSeconds(90)),
                    ct
                ) == 1;
    }
}

using ImageAI.Database;
using ImageAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImageAI.Services.Jobs;

public sealed class JobLeaser(AppDbContext db)
{
    public async Task<GenerationJob?> ClaimAsync(CancellationToken ct)
    {
        var token = Guid.NewGuid();
        // PostgreSQL atomically claims one job. Leases exceed the bounded 150s gateway call.
        var jobs = await db
            .Jobs.FromSqlInterpolated(
                $"""
                UPDATE app."GenerationJobs" SET "State" = 'Running', "LeaseToken" = {token}, "LeaseUntilUtc" = now() + interval '240 seconds', "Attempt" = "Attempt" + 1
                WHERE "Id" = (SELECT "Id" FROM app."GenerationJobs" WHERE "State" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc" <= now() ORDER BY "NextAttemptAtUtc", "Id" FOR UPDATE SKIP LOCKED LIMIT 1)
                RETURNING *
                """
            )
            .AsNoTracking()
            .ToListAsync(ct);
        return jobs.SingleOrDefault();
    }

    public async Task RecoverExpiredAsync(CancellationToken ct)
    {
        // A crashed synchronous media call may already have been billed: never silently resend.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var expired = await db
            .Jobs.Where(x => x.State == "Running" && x.LeaseUntilUtc < DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var job in expired)
        {
            var run = await db.Runs.SingleAsync(x => x.Id == job.RunId, ct);
            var p = await db.Projects.SingleAsync(x => x.Id == run.ProjectId, ct);
            job.State = run.State = p.State = "Uncertain";
            run.ErrorCode = "provider_operation_uncertain";
            p.Revision++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using ProcureAI.Application.Abstractions;
using ProcureAI.Domain.Entities;

namespace ProcureAI.Database.Persistence;

public sealed class JobStore(IDbContextFactory<AppDbContext> factory) : IJobStore
{
    public async Task<DurableJob?> ClaimAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        // A single UPDATE statement holds its row lock only until the statement commits.
        var rows = await db.Jobs.FromSqlInterpolated($"""
            UPDATE app."IngestionJobs"
            SET "State" = 'Running', "LeaseToken" = {token}, "LeaseUntilUtc" = now() + interval '180 seconds'
            WHERE "Id" = (
                SELECT "Id" FROM app."IngestionJobs"
                WHERE ("State" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc" <= now())
                   OR ("State" = 'Running' AND "LeaseUntilUtc" < now())
                ORDER BY "NextAttemptAtUtc", "Id" FOR UPDATE SKIP LOCKED LIMIT 1
            ) RETURNING *
            """).AsNoTracking().ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    public async Task ReleaseAsync(DurableJob job, string state, TimeSpan delay, string? code, string? operation, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Jobs.SingleOrDefaultAsync(x => x.Id == job.Id && x.LeaseToken == job.LeaseToken &&
            x.LeaseUntilUtc > DateTimeOffset.UtcNow, ct);
        if (current is null) return;
        current.State = state; current.LeaseToken = null; current.LeaseUntilUtc = null;
        current.NextAttemptAtUtc = DateTimeOffset.UtcNow.Add(delay); current.ErrorCode = code;
        current.ProviderOperation = operation;
        if (state == "RetryWaiting") current.Attempt++;
        if (state == "Failed" && job.DocumentId is not null && job.Kind != "Cleanup")
        {
            var document = await db.Documents.FindAsync([job.DocumentId.Value], ct);
            if (document is not null && document.State != "Deleting")
            { document.State = "Failed"; document.ErrorCode = code; }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

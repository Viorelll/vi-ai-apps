using AccessReadAI.Database;
using AccessReadAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Services.Jobs;

public sealed class JobStore(AppDbContext db)
{
    public async Task<IngestionJob?> ClaimAsync(CancellationToken ct)
    {
        var token = Guid.NewGuid();
        var jobs = await db
            .Jobs.FromSqlInterpolated(
                $"""
UPDATE app."IngestionJobs" SET "State"='Running',"LeaseToken"={token},"LeaseUntilUtc"=now()+interval '300 seconds'
WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE (("State" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("State"='Running' AND "LeaseUntilUtc"<now())) ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
"""
            )
            .ToListAsync(ct);
        return jobs.SingleOrDefault();
    }

    public static void Requeue(IngestionJob job, int delay = 1)
    {
        job.State = "Queued";
        job.LeaseUntilUtc = null;
        job.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(delay);
    }

    public static void Complete(IngestionJob job)
    {
        job.State = "Completed";
        job.LeaseUntilUtc = null;
    }
}

using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Database;
using VideoStudioAI.Domain.Entities;

namespace VideoStudioAI.Services;

public sealed class JobLeaseService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<GenerationJob?> ClaimVideoAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        return (
            await db
                .Jobs.FromSqlInterpolated(
                    $"""
                    UPDATE app."VideoGenerationJobs" SET "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '90 seconds'
                    WHERE "Id"=(SELECT "Id" FROM app."VideoGenerationJobs"
                     WHERE "State" NOT IN ('Ready','Failed','Canceled','Uncertain','NeedsReview') AND "NextPollAtUtc"<=now()
                     AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc"<now())
                     ORDER BY "NextPollAtUtc", "Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                    """
                )
                .AsNoTracking()
                .ToListAsync(ct)
        ).SingleOrDefault();
    }

    public async Task<IngestionJob?> ClaimDocumentAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        return (
            await db
                .IngestionJobs.FromSqlInterpolated(
                    $"""
                    UPDATE app."IngestionJobs" SET "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '90 seconds'
                    WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE "State"='Queued' AND "NextPollAtUtc"<=now()
                     AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc"<now()) ORDER BY "NextPollAtUtc", "Id"
                     FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                    """
                )
                .AsNoTracking()
                .ToListAsync(ct)
        ).SingleOrDefault();
    }
}

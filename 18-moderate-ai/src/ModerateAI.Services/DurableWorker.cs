using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModerateAI.Database;
using ModerateAI.Domain.Dto;
using ModerateAI.Domain.Entities;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;
using ModerateAI.Domain.Services;

namespace ModerateAI.Services;

public sealed class DurableWorker(
    IDbContextFactory<AppDbContext> factory,
    IAnalysisGateway gateway,
    IObjectStore store,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await RunOneAsync(stoppingToken))
                    await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError("Worker infrastructure failure: {Type}", e.GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    public async Task<bool> RunOneAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Guid token = Guid.NewGuid();
        var jobs = await db
            .IngestionJobs.FromSqlInterpolated(
                $"""
                UPDATE app."IngestionJobs" SET "Status"='Running',"LeaseToken"={token},"LeaseUntilUtc"=now()+interval '180 seconds',"Attempt"="Attempt"+1
                WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE (("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())) AND "Attempt"<3 ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                """
            )
            .AsNoTracking()
            .ToListAsync(ct);
        if (jobs.Count == 0)
        {
            await ExhaustExpiredAsync(db, ct);
            return false;
        }
        var job = jobs[0];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(110));
        try
        {
            if (job.Kind == "Cleanup")
            {
                var doc = await db.Documents.SingleAsync(
                    x => x.Id == job.DocumentId,
                    deadline.Token
                );
                await store.DeleteAsync(doc.ObjectKey, deadline.Token);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (await CompleteJobAsync(db, job, token, ct) > 0)
                {
                    doc.Status = "Deleted";
                    doc.Revision++;
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return true;
            }
            var item = await db.ModerationItems.SingleAsync(
                x => x.Id == job.TargetId,
                deadline.Token
            );
            if (item.Stale)
                throw new ProviderException("source_deleted", false);
            var docImage = item.ImageDocumentId is Guid imageId
                ? await db
                    .Documents.AsNoTracking()
                    .SingleAsync(x => x.Id == imageId, deadline.Token)
                : null;
            if (docImage != null && docImage.Status != "Ready")
                throw new ProviderException("source_deleted", false);
            var result = await gateway.AnalyzeAsync(
                new(job.Id, item.Text, docImage?.ObjectKey, docImage?.ContentType),
                deadline.Token
            );
            var policy = PolicyEvaluator.Evaluate(result.Snapshot);
            if (
                result.InputTokens < 0
                || result.OutputTokens < 0
                || result.InputTokens + result.OutputTokens > 15000
            )
                throw new ProviderException("token_budget_exceeded", false);
            string rawKey = $"artifacts/{item.Id}/{job.Id}.json";
            using var raw = new MemoryStream(Encoding.UTF8.GetBytes(result.Snapshot.RawJson));
            await store.PutAsync(rawKey, raw, "application/json", deadline.Token);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (await CompleteJobAsync(db, job, token, ct) == 0)
            {
                await transaction.RollbackAsync(ct);
                return true;
            }
            var analysis = new SafetyAnalysis
            {
                ItemId = item.Id,
                Provider = result.Snapshot.Provider,
                ApiVersion = result.Snapshot.ApiVersion,
                OutputScale = result.Snapshot.OutputScale,
                RawObjectKey = rawKey,
                Complete = result.Snapshot.Complete,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
            };
            db.Add(analysis);
            foreach (
                var score in result.Snapshot.Scores.GroupBy(x => x.Category).Select(x => x.First())
            )
                db.Add(
                    new CategoryFinding
                    {
                        AnalysisId = analysis.Id,
                        Category = score.Category,
                        Severity = score.Severity,
                    }
                );
            db.Add(
                new PolicyDecision
                {
                    ItemId = item.Id,
                    PolicyVersion = policy.PolicyVersion,
                    Decision = policy.Decision,
                    ReasonsJson = JsonSerializer.Serialize(policy.Reasons),
                    DecisionHash = policy.DecisionHash,
                }
            );
            item.Explanation = result.Explanation;
            item.State = policy.Decision switch
            {
                "allow" => "Allowed",
                "block" => "Blocked",
                _ => "Review",
            };
            item.Revision++;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            db.ChangeTracker.Clear();
            bool transient =
                e
                is ProviderException { Transient: true }
                    or HttpRequestException
                    or OperationCanceledException;
            string code = e switch
            {
                ProviderException p => p.Code,
                DbUpdateConcurrencyException => "source_or_revision_changed",
                _ => "analysis_failed",
            };
            int delay = e is ProviderException p2 ? Math.Clamp(p2.RetryAfterSeconds, 0, 120) : 0;
            await FailAsync(db, job, token, code, transient, delay, ct);
            logger.LogWarning(
                "Job {Id}: {Code}, attempt {Attempt}, type {Type}, stack {Stack}",
                job.Id,
                code,
                job.Attempt,
                e.GetType().Name,
                e.StackTrace
            );
        }
        return true;
    }

    private static Task<int> CompleteJobAsync(
        AppDbContext db,
        IngestionJob job,
        Guid token,
        CancellationToken ct
    ) =>
        db
            .IngestionJobs.Where(x =>
                x.Id == job.Id
                && x.LeaseToken == token
                && x.Status == "Running"
                && x.LeaseUntilUtc > DateTimeOffset.UtcNow
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Completed")
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                ct
            );

    private static async Task FailAsync(
        AppDbContext db,
        IngestionJob job,
        Guid token,
        string code,
        bool transient,
        int delay,
        CancellationToken ct
    )
    {
        bool retry = transient && job.Attempt < 3;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        int changed = await db
            .IngestionJobs.Where(x =>
                x.Id == job.Id && x.LeaseToken == token && x.Status == "Running"
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, retry ? "RetryWaiting" : "Failed")
                        .SetProperty(x => x.ErrorCode, code)
                        .SetProperty(
                            x => x.NextAttemptAtUtc,
                            DateTimeOffset.UtcNow.AddSeconds(
                                Math.Max(
                                    delay,
                                    Math.Pow(2, job.Attempt) + Random.Shared.NextDouble()
                                )
                            )
                        )
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                ct
            );
        if (changed > 0 && !retry && job.TargetId != null)
            await db
                .ModerationItems.Where(x => x.Id == job.TargetId)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.State, "Failed")
                            .SetProperty(x => x.ErrorCode, code)
                            .SetProperty(x => x.Revision, x => x.Revision + 1),
                    ct
                );
        await tx.CommitAsync(ct);
    }

    private static async Task ExhaustExpiredAsync(AppDbContext db, CancellationToken ct)
    {
        var expired = await db
            .IngestionJobs.AsNoTracking()
            .Where(x =>
                x.Status == "Running" && x.LeaseUntilUtc < DateTimeOffset.UtcNow && x.Attempt >= 3
            )
            .ToListAsync(ct);
        foreach (var job in expired)
            if (job.LeaseToken is Guid token)
                await FailAsync(db, job, token, "retry_exhausted", false, 0, ct);
    }
}

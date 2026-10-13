using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Api.Services;

public sealed class DurableWorker(
    IDbContextFactory<AppDbContext> factory,
    IServiceScopeFactory scopes,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await TickAsync(stoppingToken))
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogWarning("Worker unavailable: {Type}", e.GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    public async Task<bool> TickAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        Guid token = Guid.NewGuid();
        var jobs = await db
            .Jobs.FromSqlInterpolated(
                $"""
                UPDATE app."IngestionJobs" SET "Status"='Running', "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '150 seconds'
                WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE (("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())) ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                """
            )
            .AsNoTracking()
            .ToListAsync(ct);
        var job = jobs.SingleOrDefault();
        if (job == null)
            return false;
        await using var scope = scopes.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStore>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiClient>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(110));
        try
        {
            var doc = await db.Documents.FindAsync([job.DocumentId], ct);
            if (doc == null)
            {
                await FinishAsync(db, job, token, "Completed", null, ct);
                return true;
            }
            if (job.Kind == "Cleanup")
            {
                await storage.DeleteAsync(doc.ObjectKey, deadline.Token);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                if (!await OwnAsync(db, job.Id, token, ct))
                    return true;
                await db.Chunks.Where(x => x.DocumentId == doc.Id).ExecuteDeleteAsync(ct);
                doc.Status = "Deleted";
                doc.ActiveGeneration = 0;
                await FinishAsync(db, job, token, "Completed", null, ct);
                await tx.CommitAsync(ct);
                return true;
            }
            if (doc.Status is "Deleting" or "Deleted")
            {
                await FinishAsync(db, job, token, "Completed", null, ct);
                return true;
            }
            var result = await ai.ExtractAsync(
                new(doc.Id, doc.ObjectKey, doc.ContentType, job.OperationUrl),
                deadline.Token
            );
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (!await OwnAsync(db, job.Id, token, ct))
                return true;
            await db.Entry(doc).ReloadAsync(ct);
            if (doc.Status is "Deleting" or "Deleted")
            {
                await FinishAsync(db, job, token, "Completed", null, ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            if (result.Status == "Running")
            {
                doc.Status = "Processing";
                await db
                    .Jobs.Where(x => x.Id == job.Id && x.LeaseToken == token)
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Status, "Queued")
                                .SetProperty(x => x.OperationUrl, result.OperationUrl)
                                .SetProperty(
                                    x => x.NextAttemptAtUtc,
                                    DateTimeOffset.UtcNow.AddSeconds(2)
                                )
                                .SetProperty(x => x.LeaseToken, (Guid?)null),
                        ct
                    );
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return true;
            }
            if (
                result.Status != "Completed"
                || result.Sources.Count == 0
                || result.Sources.Any(x =>
                    x.DocumentId != doc.Id || string.IsNullOrWhiteSpace(x.Text)
                )
            )
                throw new DomainException("invalid_extraction", 422);
            int generation = doc.ActiveGeneration + 1;
            int ordinal = 0;
            foreach (var source in result.Sources.Take(200))
                db.Chunks.Add(
                    new DocumentChunk
                    {
                        DocumentId = doc.Id,
                        Generation = generation,
                        Ordinal = ordinal++,
                        Text = source.Text[..Math.Min(source.Text.Length, 12000)],
                        PageNumber = source.PageNumber,
                    }
                );
            doc.ActiveGeneration = generation;
            doc.Status = "Ready";
            doc.ErrorCode = null;
            await FinishAsync(db, job, token, "Completed", null, ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            db.ChangeTracker.Clear();
            string code = e is DomainException d ? d.Code : "provider_unavailable";
            bool transient =
                e is not DomainException de || de.Status >= 500 || de.Code.Contains("429");
            job.Attempt++;
            string status = transient && job.Attempt < 3 ? "RetryWaiting" : "Failed";
            await db
                .Jobs.Where(x => x.Id == job.Id && x.LeaseToken == token)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, status)
                            .SetProperty(x => x.Attempt, job.Attempt)
                            .SetProperty(x => x.ErrorCode, code)
                            .SetProperty(
                                x => x.NextAttemptAtUtc,
                                DateTimeOffset.UtcNow.AddSeconds(
                                    Math.Pow(2, job.Attempt) * 2 + Random.Shared.NextDouble()
                                )
                            )
                            .SetProperty(x => x.LeaseToken, (Guid?)null),
                    ct
                );
            if (status == "Failed")
                await db
                    .Documents.Where(x =>
                        x.Id == job.DocumentId && x.Status != "Deleting" && x.Status != "Deleted"
                    )
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Status, "Failed")
                                .SetProperty(x => x.ErrorCode, code),
                        ct
                    );
            logger.LogWarning("Job {JobId} {Status}: {Code}", job.Id, status, code);
        }
        return true;
    }

    private static async Task<bool> OwnAsync(
        AppDbContext db,
        Guid id,
        Guid token,
        CancellationToken ct
    ) =>
        await db
            .Jobs.Where(x => x.Id == id && x.LeaseToken == token)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LeaseUntilUtc, DateTimeOffset.UtcNow.AddSeconds(150)),
                ct
            ) == 1;

    private static async Task FinishAsync(
        AppDbContext db,
        IngestionJob job,
        Guid token,
        string state,
        string? error,
        CancellationToken ct
    )
    {
        await db
            .Jobs.Where(x => x.Id == job.Id && x.LeaseToken == token)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, state)
                        .SetProperty(x => x.ErrorCode, error)
                        .SetProperty(x => x.LeaseToken, (Guid?)null),
                ct
            );
        await db.SaveChangesAsync(ct);
    }
}

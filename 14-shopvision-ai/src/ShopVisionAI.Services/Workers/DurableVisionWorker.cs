using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShopVisionAI.Contracts.Dto;
using ShopVisionAI.Contracts.Validation;
using ShopVisionAI.Database;
using ShopVisionAI.Services.Clients;
using ShopVisionAI.Services.Matching;
using ShopVisionAI.Services.Searches;

namespace ShopVisionAI.Services.Workers;

public sealed class DurableVisionWorker(
    IDbContextFactory<AppDbContext> factory,
    DomainAiClient ai,
    MatchingService matching,
    ILogger<DurableVisionWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await RunOnceAsync(stoppingToken))
                    await Task.Delay(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Worker cycle failed");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    public async Task<bool> RunOnceAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            WITH exhausted AS (UPDATE app."IngestionJobs" SET "Status"='Failed', "LeaseUntilUtc"=NULL WHERE "Status"='Running' AND "LeaseUntilUtc"<now() AND "Attempt">=3 RETURNING "TargetId")
            UPDATE app."VisualSearches" SET "State"='Failed', "ErrorCode"='retry_limit_exceeded', "Revision"="Revision"+1 WHERE "Id" IN (SELECT "TargetId" FROM exhausted) AND "State" IN ('Inspecting','Searching')
            """,
            ct
        );
        var token = Guid.NewGuid();
        var jobs = await db
            .IngestionJobs.FromSqlInterpolated(
                $"""
                UPDATE app."IngestionJobs" SET "Status"='Running', "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '150 seconds', "Attempt"="Attempt"+1
                WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE (("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())) AND "Attempt"<3 ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                """
            )
            .AsNoTracking()
            .ToListAsync(ct);
        if (jobs.Count == 0)
            return false;
        var job = jobs[0];
        try
        {
            var search = await db
                .VisualSearches.Include(x => x.ImageDocument)
                .SingleAsync(x => x.Id == job.TargetId, ct);
            if (search.ImageDocument.Status != "Ready")
                throw new ArgumentException("source_deleted");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(110));
            var source = "image:" + search.ImageDocumentId;
            var output = await ai.InspectAsync(
                new(
                    job.Id,
                    source,
                    search.ImageDocument.ObjectKey,
                    search.ImageDocument.Mime,
                    search.Question
                ),
                deadline.Token
            );
            VisionValidator.Validate(output, source);
            var products = await db
                .CatalogProducts.AsNoTracking()
                .Where(x => x.CatalogVersion == search.CatalogVersion)
                .ToListAsync(ct);
            var candidates = matching.Rank(output.Observations, products, DateTimeOffset.UtcNow);
            GroundedResponse? grounded = null;
            if (candidates.Count > 0)
            {
                grounded = await ai.GenerateAsync(
                    new(
                        job.Id,
                        "Explică candidații și limitele identificării.",
                        output.Observations,
                        candidates
                    ),
                    deadline.Token
                );
                GroundingValidator.Validate(grounded, candidates);
                if (
                    output.InputTokens
                        + output.OutputTokens
                        + grounded.InputTokens
                        + grounded.OutputTokens
                    > 15000
                )
                    throw new ArgumentException("token_budget_exceeded");
                candidates = candidates
                    .Select(c =>
                        c with
                        {
                            Reason = grounded.Explanations.Single(e => e.Sku == c.Sku).Reason,
                        }
                    )
                    .ToArray();
                output = output with
                {
                    InputTokens = output.InputTokens + grounded.InputTokens,
                    OutputTokens = output.OutputTokens + grounded.OutputTokens,
                };
            }
            var result = new SearchResult(
                output.Observations,
                candidates,
                "Candidați, nu identificare garantată. Stocul și prețul provin din catalogul seed, nu din fotografie.",
                search.CatalogVersion,
                output.Demonstration
            );
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var owned = await db
                .IngestionJobs.Where(x =>
                    x.Id == job.Id
                    && x.LeaseToken == token
                    && x.LeaseUntilUtc > DateTimeOffset.UtcNow
                )
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, "Completed")
                            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                    ct
                );
            if (owned == 0)
                return true;
            var json = JsonSerializer.Serialize(result, VisualSearchService.Json);
            await db
                .VisualSearches.Where(x => x.Id == search.Id && x.State != "Stale")
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(
                                x => x.State,
                                candidates.Count == 0 ? "NeedsInput" : "Completed"
                            )
                            .SetProperty(x => x.ResultJson, json)
                            .SetProperty(
                                x => x.InputTokens,
                                x => x.InputTokens + output.InputTokens
                            )
                            .SetProperty(
                                x => x.OutputTokens,
                                x => x.OutputTokens + output.OutputTokens
                            )
                            .SetProperty(x => x.Revision, x => x.Revision + 1),
                    ct
                );
            await tx.CommitAsync(ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Vision operation {OperationId} failed: {ErrorType}",
                job.Id,
                e.GetType().Name
            );
            var transient =
                e is HttpRequestException h
                    && (
                        h.StatusCode == null || (int)h.StatusCode >= 500 || (int)h.StatusCode == 429
                    )
                || e is TaskCanceledException;
            var retry = transient && job.Attempt < 3;
            var delay = Math.Pow(2, job.Attempt) + Random.Shared.NextDouble();
            if (
                e is ShopVisionAI.Infrastructure.Http.ProviderCallException p
                && p.RetryAfter is { } retryAfter
            )
                delay = Math.Max(delay, Math.Clamp(retryAfter.TotalSeconds, 0, 120));
            var next = DateTimeOffset.UtcNow.AddSeconds(delay);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var owned = await db
                .IngestionJobs.Where(x => x.Id == job.Id && x.LeaseToken == token)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.Status, retry ? "RetryWaiting" : "Failed")
                            .SetProperty(x => x.NextAttemptAtUtc, next)
                            .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                    ct
                );
            if (owned > 0 && !retry)
                await db
                    .VisualSearches.Where(x => x.Id == job.TargetId && x.State != "Stale")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.State, "Failed")
                                .SetProperty(x => x.ErrorCode, "vision_provider_failed")
                                .SetProperty(x => x.Revision, x => x.Revision + 1),
                        ct
                    );
            await tx.CommitAsync(ct);
        }
        return true;
    }
}

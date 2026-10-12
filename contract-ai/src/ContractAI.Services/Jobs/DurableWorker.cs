using System.Text.Json;
using System.Text.RegularExpressions;
using ContractAI.Database.Jobs;
using ContractAI.Database.Persistence;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Entities;
using ContractAI.Domain.Models;
using ContractAI.Domain.Services;
using ContractAI.Infrastructure.Abstractions;
using ContractAI.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractAI.Services.Jobs;

public sealed class DurableWorker(
    IDbContextFactory<AppDbContext> factory,
    JobStore jobs,
    IAiClient ai,
    IObjectStore storage,
    ILogger<DurableWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await jobs.ClaimAsync(stoppingToken);
                if (job is null)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(70));
                try
                {
                    await ProcessAsync(job, deadline.Token);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await FailAsync(job, ex, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Worker unavailable: {Type}", ex.GetType().Name);
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task ProcessAsync(IngestionJob job, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (job.Kind == "Extract")
        {
            await ExtractAsync(db, job, ct);
            return;
        }
        if (job.Kind == "Cleanup")
        {
            var doc = await db.Documents.SingleAsync(x => x.Id == job.DocumentId, ct);
            await storage.DeleteAsync(doc.ObjectKey, ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/contract-v1/normalized.json", ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/contract-v1/raw.json", ct);
            await FinishAsync(db, job, ct);
            return;
        }
        if (job.Kind == "Analyze")
        {
            var v = await db.ContractVersions.SingleAsync(x => x.Id == job.TargetId, ct);
            var doc = await db.Documents.SingleAsync(
                x => x.Id == v.DocumentId && x.Status == "Ready",
                ct
            );
            var chunks = await db
                .DocumentChunks.Where(x =>
                    x.DocumentId == doc.Id && x.Generation == doc.ActiveGeneration
                )
                .OrderBy(x => x.Ordinal)
                .ToArrayAsync(ct);
            var old = await db.Clauses.Where(x => x.VersionId == v.Id).ToArrayAsync(ct);
            // Persist clause identity before cloud calls so a reclaim reuses evidence IDs.
            if (old.Length == 0)
            {
                int span = 0;
                foreach (var chunk in chunks)
                {
                    db.Add(
                        new Clause
                        {
                            VersionId = v.Id,
                            ChunkId = chunk.Id,
                            Ordinal = chunk.Ordinal,
                            Text = chunk.Text,
                            PageNumber = chunk.PageNumber,
                            SpanStart = span,
                            SpanLength = chunk.Text.Length,
                        }
                    );
                    span += chunk.Text.Length + 2;
                }
                if (!await jobs.RenewAsync(job, ct))
                    return;
                await db.SaveChangesAsync(ct);
            }
            var sources = await ContractService.SourcesAsync(db, [v.Id], true, ct);
            var hash = ContractService.Hash(
                JsonSerializer.Serialize(sources, ContractService.Json)
            );
            var raw = await ai.StepAsync(new("analyze", job.Id, hash, "contract-v1", sources), ct);
            var result =
                JsonSerializer.Deserialize<AnalysisResult>(raw.OutputJson, ContractService.Json)
                ?? throw new AppException(502, "invalid_analysis", "Empty analysis.");
            GroundingValidator.ValidateAnalysis(result, sources);
            await BeginGuardedAsync(db, job, ct);
            await ContractService.SourcesAsync(db, [v.Id], true, ct);
            await db.Obligations.Where(x => x.VersionId == v.Id).ExecuteDeleteAsync(ct);
            await db.Findings.Where(x => x.VersionId == v.Id).ExecuteDeleteAsync(ct);
            foreach (var item in result.Obligations)
                db.Add(
                    new Obligation
                    {
                        VersionId = v.Id,
                        ClauseId = item.ClauseId,
                        PartyText = item.Party,
                        ActionText = item.Action,
                        DueDate = item.DueDate,
                        RelativeTerm = item.RelativeTerm,
                        EvidenceJson = JsonSerializer.Serialize(
                            item.Evidence,
                            ContractService.Json
                        ),
                    }
                );
            foreach (var item in result.Findings)
                db.Add(
                    new Finding
                    {
                        VersionId = v.Id,
                        ClauseId = item.Evidence.First().ClauseId,
                        Code = item.Code,
                        Severity = item.Severity,
                        Explanation = item.Explanation,
                        EvidenceJson = JsonSerializer.Serialize(
                            item.Evidence,
                            ContractService.Json
                        ),
                    }
                );
            v.Summary = result.Summary;
            v.UnknownsJson = JsonSerializer.Serialize(result.Unknowns, ContractService.Json);
            v.State = result.Findings.Length > 0 ? "NeedsReview" : "Ready";
            v.Revision++;
            v.InputHash = hash;
            v.Deployment = raw.Deployment;
            v.InputTokens = raw.InputTokens;
            v.OutputTokens = raw.OutputTokens;
            await FinishAsync(db, job, ct);
            return;
        }
        if (job.Kind == "Compare")
        {
            var comparison = await db.Comparisons.SingleAsync(x => x.Id == job.TargetId, ct);
            var sources = await ContractService.SourcesAsync(
                db,
                [comparison.LeftVersionId, comparison.RightVersionId],
                true,
                ct
            );
            var pairs = ClauseComparer.Compare(
                sources.Where(x => x.VersionId == comparison.LeftVersionId).ToArray(),
                sources.Where(x => x.VersionId == comparison.RightVersionId).ToArray()
            );
            var raw = await ai.StepAsync(
                new("compare", job.Id, comparison.InputHash, "contract-v1", sources, Pairs: pairs),
                ct
            );
            var result =
                JsonSerializer.Deserialize<ComparisonResult>(raw.OutputJson, ContractService.Json)
                ?? throw new AppException(502, "invalid_comparison", "Empty comparison.");
            if (
                string.IsNullOrWhiteSpace(result.Summary)
                || result.Findings is null
                || result.Findings.Length > 100
            )
                throw new AppException(502, "invalid_comparison", "Invalid comparison schema.");
            foreach (var finding in result.Findings)
            {
                GroundingValidator.ValidateFinding(finding, sources);
                var matched = pairs.FirstOrDefault(p =>
                    p.Kind != "unchanged"
                    && (
                        p.LeftClauseId == finding.Evidence[0].ClauseId
                        || p.RightClauseId == finding.Evidence[0].ClauseId
                    )
                );
                if (
                    matched is null
                    || (
                        matched.Kind == "modified"
                        && (
                            !finding.Evidence.Any(e => e.ClauseId == matched.LeftClauseId)
                            || !finding.Evidence.Any(e => e.ClauseId == matched.RightClauseId)
                        )
                    )
                )
                    throw new AppException(
                        502,
                        "comparison_evidence",
                        "A modified pair requires citations from both versions."
                    );
            }
            await BeginGuardedAsync(db, job, ct);
            await ContractService.SourcesAsync(
                db,
                [comparison.LeftVersionId, comparison.RightVersionId],
                true,
                ct
            );
            comparison.State = "Ready";
            comparison.Deployment = raw.Deployment;
            comparison.InputTokens = raw.InputTokens;
            comparison.OutputTokens = raw.OutputTokens;
            comparison.ResultJson = JsonSerializer.Serialize(
                new
                {
                    result.Summary,
                    result.Findings,
                    pairs,
                    citations = sources.Select(s => ContractService.Cite(s, s.Text)),
                },
                ContractService.Json
            );
            await FinishAsync(db, job, ct);
            return;
        }
        throw new AppException(422, "unknown_job", "Unsupported durable job kind.");
    }

    private async Task ExtractAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var doc = await db.Documents.SingleAsync(x => x.Id == job.DocumentId, ct);
        if (doc.Status == "Deleting")
        {
            await FinishAsync(db, job, ct);
            return;
        }
        var result = job.OperationUrl is null
            ? await ai.StartAsync(new(doc.Id, doc.ObjectKey, doc.ContentType), ct)
            : await ai.PollAsync(new(job.OperationUrl), ct);
        if (result.Status == "Failed")
            throw new AppException(
                422,
                result.ErrorCode ?? "extraction_failed",
                "Document extraction failed."
            );
        if (result.Status == "Running")
        {
            await BeginGuardedAsync(db, job, ct);
            var stored = await db.IngestionJobs.SingleAsync(x => x.Id == job.Id, ct);
            stored.OperationUrl = result.OperationUrl;
            stored.Status = "RetryWaiting";
            stored.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(2);
            stored.LeaseToken = null;
            stored.LeaseUntilUtc = null;
            doc.Status = "Processing";
            await db.SaveChangesAsync(ct);
            await db.Database.CommitTransactionAsync(ct);
            return;
        }
        var pages =
            result.Pages ?? throw new AppException(422, "empty_extraction", "No extracted pages.");
        var texts = pages
            .SelectMany(p =>
                Regex
                    .Split(p.Text.Replace("\r", ""), @"\n\s*\n")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => new ExtractedPage(p.PageNumber, t.Trim(), p.RegionJson))
            )
            .ToArray();
        if (texts.Length == 0 || texts.Length > 200 || texts.Sum(x => x.Text.Length) > 100000)
            throw new AppException(
                422,
                "extraction_context_limit",
                "Document exceeds the NoRag context limits (200 clauses/100000 characters), or contains no text."
            );
        if (result.RawJson is not null)
        {
            using var rawArtifact = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(result.RawJson)
            );
            await storage.PutAsync(
                $"artifacts/{doc.Id}/contract-v1/raw.json",
                rawArtifact,
                "application/json",
                ct
            );
        }
        using var artifact = new MemoryStream(
            JsonSerializer.SerializeToUtf8Bytes(pages, ContractService.Json)
        );
        await storage.PutAsync(
            $"artifacts/{doc.Id}/contract-v1/normalized.json",
            artifact,
            "application/json",
            ct
        );
        await BeginGuardedAsync(db, job, ct);
        var generation = doc.ActiveGeneration + 1;
        for (int i = 0; i < texts.Length; i++)
            db.Add(
                new DocumentChunk
                {
                    DocumentId = doc.Id,
                    Generation = generation,
                    Ordinal = i,
                    Text = texts[i].Text,
                    PageNumber = texts[i].PageNumber,
                    RegionJson = texts[i].RegionJson,
                }
            );
        doc.ActiveGeneration = generation;
        doc.Status = "Ready";
        doc.ErrorCode = null;
        await FinishAsync(db, job, ct);
    }

    private static async Task BeginGuardedAsync(
        AppDbContext db,
        IngestionJob job,
        CancellationToken ct
    )
    {
        await db.Database.BeginTransactionAsync(ct);
        // Lock the lease row through commit. A stale worker cannot publish output.
        var rows = await db
            .IngestionJobs.FromSqlInterpolated(
                $"SELECT * FROM app.\"IngestionJobs\" WHERE \"Id\" = {job.Id} AND \"LeaseToken\" = {job.LeaseToken} AND \"Status\" = 'Running' AND \"LeaseUntilUtc\" > now() FOR UPDATE"
            )
            .AsNoTracking()
            .ToArrayAsync(ct);
        if (rows.Length != 1)
            throw new AppException(409, "lease_lost", "Worker lease expired or was revoked.");
    }

    private static async Task FinishAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            await BeginGuardedAsync(db, job, ct);
        await db
            .IngestionJobs.Where(x => x.Id == job.Id && x.LeaseToken == job.LeaseToken)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Succeeded")
                        .SetProperty(x => x.LeaseToken, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null),
                ct
            );
        await db.SaveChangesAsync(ct);
        await db.Database.CommitTransactionAsync(ct);
    }

    private async Task FailAsync(IngestionJob job, Exception ex, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var stored = await db.IngestionJobs.SingleOrDefaultAsync(
            x => x.Id == job.Id && x.LeaseToken == job.LeaseToken && x.Status == "Running",
            ct
        );
        if (stored is null)
            return;
        var code =
            ex is AppException a ? a.Code
            : ex is JsonException ? "invalid_ai_json"
            : ex is OperationCanceledException ? "operation_timeout"
            : "job_failed";
        var transient =
            ex is HttpRequestException or OperationCanceledException
            || ex is AppException { Status: 503 }
                && code is not ("foundry_not_configured" or "extractor_not_configured");
        stored.Attempt++;
        stored.ErrorCode = code;
        stored.LeaseToken = null;
        stored.LeaseUntilUtc = null;
        stored.Status = transient && stored.Attempt < 3 ? "RetryWaiting" : "Failed";
        stored.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(
            Math.Max(
                Math.Pow(2, stored.Attempt) + Random.Shared.NextDouble(),
                (ex as AppException)?.RetryAfterSeconds ?? 0
            )
        );
        if (stored.Status == "Failed")
        {
            if (job.Kind == "Extract")
                await db
                    .Documents.Where(x => x.Id == job.DocumentId && x.Status != "Deleting")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.Status, "Failed")
                                .SetProperty(x => x.ErrorCode, code),
                        ct
                    );
            if (job.Kind == "Analyze")
                await db
                    .ContractVersions.Where(x => x.Id == job.TargetId && x.State != "Stale")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.State, "Failed")
                                .SetProperty(x => x.Revision, x => x.Revision + 1),
                        ct
                    );
            if (job.Kind == "Compare")
                await db
                    .Comparisons.Where(x => x.Id == job.TargetId && x.State != "Stale")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Failed"), ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogWarning(
            "Job {Job} {Kind} failed with {Code}, attempt {Attempt}",
            job.Id,
            job.Kind,
            code,
            stored.Attempt
        );
    }
}

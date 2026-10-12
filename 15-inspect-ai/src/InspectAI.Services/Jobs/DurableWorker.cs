using System.Text.Json;
using System.Text.RegularExpressions;
using InspectAI.Database.Jobs;
using InspectAI.Database.Persistence;
using InspectAI.Domain.DTOs;
using InspectAI.Domain.Entities;
using InspectAI.Domain.Models;
using InspectAI.Domain.Services;
using InspectAI.Infrastructure.Abstractions;
using InspectAI.Services.Inspections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InspectAI.Services.Jobs;

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
            if (doc.PhotoObjectKey is not null)
                await storage.DeleteAsync(doc.PhotoObjectKey, ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/inspect-v1/normalized.json", ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/inspect-v1/raw.json", ct);
            await FinishAsync(db, job, ct);
            return;
        }
        if (job.Kind == "Analyze")
        {
            var inspection = await InspectionService.GetEntityAsync(db, job.TargetId!.Value, ct);
            var (photos, sources) = await InspectionService.ScopeAsync(db, inspection, ct);
            var hash = InspectionService.InputHash(inspection, photos, sources);
            var previous = await db.InspectionReports.SingleOrDefaultAsync(
                x =>
                    x.InspectionId == inspection.Id
                    && x.InputHash == hash
                    && x.PromptVersion == "inspect-v1",
                ct
            );
            AiResult raw;
            InspectionResult result;
            if (previous is not null)
            {
                result = JsonSerializer.Deserialize<InspectionResult>(
                    previous.ResultJson,
                    InspectionService.Json
                )!;
                raw = new(previous.ResultJson, previous.Deployment, 0, 0, 0);
            }
            else
            {
                raw = await ai.StepAsync(
                    new(
                        "analyze",
                        job.Id,
                        hash,
                        "inspect-v1",
                        photos,
                        sources,
                        inspection.EquipmentModel,
                        inspection.ManualVersion
                    ),
                    ct
                );
                result =
                    JsonSerializer.Deserialize<InspectionResult>(
                        raw.OutputJson,
                        InspectionService.Json
                    ) ?? throw new AppException(502, "invalid_report", "No report returned.");
            }
            GroundingValidator.Validate(result, photos, sources);
            await BeginGuardedAsync(db, job, ct);
            var (currentPhotos, currentSources) = await InspectionService.ScopeAsync(
                db,
                inspection,
                ct
            );
            if (hash != InspectionService.InputHash(inspection, currentPhotos, currentSources))
                throw new AppException(409, "sources_changed", "Sources changed during analysis.");
            await db
                .ChecklistItems.Where(x => x.InspectionId == inspection.Id)
                .ExecuteDeleteAsync(ct);
            await db
                .VisualFindings.Where(x => x.InspectionId == inspection.Id)
                .ExecuteDeleteAsync(ct);
            foreach (var f in result.Findings)
                db.Add(
                    new VisualFinding
                    {
                        Id = f.Id,
                        InspectionId = inspection.Id,
                        DocumentId = f.DocumentId,
                        Kind = f.Kind,
                        Description = f.Description,
                        Reason = f.Reason,
                        RegionJson = f.Region is null
                            ? null
                            : JsonSerializer.Serialize(f.Region, InspectionService.Json),
                    }
                );
            foreach (var c in result.Checklist)
                db.Add(
                    new ChecklistItem
                    {
                        Id = c.Id,
                        InspectionId = inspection.Id,
                        FindingId = c.FindingId,
                        Text = c.Text,
                        EvidenceJson = JsonSerializer.Serialize(c.Evidence, InspectionService.Json),
                    }
                );
            if (previous is null)
                db.Add(
                    new InspectionReport
                    {
                        InspectionId = inspection.Id,
                        InputHash = hash,
                        ResultJson = JsonSerializer.Serialize(result, InspectionService.Json),
                        SourcesJson = JsonSerializer.Serialize(
                            new { photos, sources },
                            InspectionService.Json
                        ),
                        Deployment = raw.Deployment,
                        InputTokens = raw.InputTokens,
                        OutputTokens = raw.OutputTokens,
                    }
                );
            inspection.State = "NeedsReview";
            inspection.Revision++;
            inspection.ErrorCode = null;
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
        if (doc.ContentType.StartsWith("image/"))
        {
            await BeginGuardedAsync(db, job, ct);
            doc.Status = "Ready";
            doc.ActiveGeneration = 1;
            doc.ErrorCode = null;
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
                "Document exceeds the NoRag context limits (200 chunks/100000 characters), or contains no text."
            );
        if (result.RawJson is not null)
        {
            using var rawArtifact = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(result.RawJson)
            );
            await storage.PutAsync(
                $"artifacts/{doc.Id}/inspect-v1/raw.json",
                rawArtifact,
                "application/json",
                ct
            );
        }
        using var artifact = new MemoryStream(
            JsonSerializer.SerializeToUtf8Bytes(pages, InspectionService.Json)
        );
        await storage.PutAsync(
            $"artifacts/{doc.Id}/inspect-v1/normalized.json",
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
                    .Inspections.Where(x => x.Id == job.TargetId && x.State != "Stale")
                    .ExecuteUpdateAsync(
                        s =>
                            s.SetProperty(x => x.State, "Failed")
                                .SetProperty(x => x.ErrorCode, code)
                                .SetProperty(x => x.Revision, x => x.Revision + 1),
                        ct
                    );
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

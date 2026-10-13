using System.Text;
using System.Text.Json;
using EvalAI.Database.Jobs;
using EvalAI.Database.Persistence;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using EvalAI.Domain.Services;
using EvalAI.Infrastructure.Abstractions;
using EvalAI.Services.Evaluations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace EvalAI.Services.Jobs;

public sealed class DurableWorker(JobStore store, IDbContextFactory<AppDbContext> factory, IAiClient ai, IObjectStore storage, ILogger<DurableWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IngestionJob? job = null;
            try
            {
                job = await store.ClaimAsync(stoppingToken);
                if (job is null) { await Task.Delay(400, stoppingToken); continue; }
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken); deadline.CancelAfter(TimeSpan.FromSeconds(70));
                if (job.Attempt >= 3) throw new AppException(422, "lease_reclaim_limit", "Repeated lease loss; inspect provider usage before retry.");
                await ProcessAsync(job, deadline.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Worker failed: {Type}", ex.GetType().Name); if (job != null) await FailAsync(job, ex, stoppingToken); else await Task.Delay(2000, stoppingToken); }
        }
    }
    private async Task ProcessAsync(IngestionJob job, CancellationToken ct)
    {
        if (job.Kind == "Evaluate") { await EvaluateAsync(job, ct); return; }
        await using var db = await factory.CreateDbContextAsync(ct); var doc = await db.Documents.SingleAsync(d => d.Id == job.DocumentId, ct);
        if (job.Kind == "Cleanup")
        {
            await storage.DeleteAsync(doc.ObjectKey, ct); await storage.DeleteAsync($"artifacts/{doc.Id}/eval-v1/raw.json", ct); await storage.DeleteAsync($"artifacts/{doc.Id}/eval-v1/normalized.json", ct);
            await GuardAsync(db, job, ct); await db.DocumentChunks.Where(c => c.DocumentId == doc.Id).ExecuteDeleteAsync(ct); await FinishAsync(db, job, false, ct); return;
        }
        if (job.Kind != "Extract") throw new AppException(422, "unknown_job", "Unsupported job kind.");
        var result = job.OperationUrl is null ? await ai.StartAsync(new(doc.Id, doc.ObjectKey, doc.ContentType), ct) : await ai.PollAsync(new(job.OperationUrl), ct);
        if (result.Status == "Failed") throw new AppException(422, result.ErrorCode ?? "extraction_failed", "Extraction failed.");
        if (result.Status == "Running")
        {
            await GuardAsync(db, job, ct); await db.Entry(doc).ReloadAsync(ct); if (doc.Status == "Deleting") throw new AppException(409, "document_deleted", "Document deleted.");
            var stored = await db.IngestionJobs.SingleAsync(j => j.Id == job.Id, ct); stored.OperationUrl = result.OperationUrl; doc.Status = "Processing"; await FinishAsync(db, job, true, ct, 2); return;
        }
        var pages = result.Pages ?? throw new AppException(422, "empty_extraction", "No extracted text.");
        var parts = pages.SelectMany(p => Enumerable.Range(0, (p.Text.Length + 1999) / 2000).Select(i => new ExtractedPage(p.PageNumber, p.Text.Substring(i * 2000, Math.Min(2000, p.Text.Length - i * 2000)), p.RegionJson))).Where(p => !string.IsNullOrWhiteSpace(p.Text)).ToArray();
        if (parts.Length is < 1 or > 200 || parts.Sum(p => p.Text.Length) > 100000) throw new AppException(422, "extraction_context_limit", "NoRag accepts 1-200 chunks and at most 100000 characters.");
        using var artifact = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(parts, JsonDefaults.Options)); await storage.PutAsync($"artifacts/{doc.Id}/eval-v1/normalized.json", artifact, "application/json", ct);
        if (result.RawJson != null) { using var raw = new MemoryStream(Encoding.UTF8.GetBytes(result.RawJson)); await storage.PutAsync($"artifacts/{doc.Id}/eval-v1/raw.json", raw, "application/json", ct); }
        await GuardAsync(db, job, ct); await db.Entry(doc).ReloadAsync(ct); if (doc.Status == "Deleting") throw new AppException(409, "document_deleted", "Document deleted.");
        var generation = doc.ActiveGeneration + 1;
        for (int i = 0; i < parts.Length; i++) db.Add(new DocumentChunk { DocumentId = doc.Id, Generation = generation, Ordinal = i, Text = parts[i].Text, PageNumber = parts[i].PageNumber, RegionJson = parts[i].RegionJson });
        doc.ActiveGeneration = generation; doc.Status = "Ready"; doc.ErrorCode = null; await FinishAsync(db, job, false, ct);
    }
    private async Task EvaluateAsync(IngestionJob job, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct); var run = await db.EvalRuns.SingleAsync(r => r.Id == job.TargetId, ct);
        if (run.State == "Canceled") return;
        var result = await db.EvalCaseResults.Where(r => r.RunId == run.Id && r.State != "Completed" && r.State != "Failed").OrderBy(r => r.VariantId).ThenBy(r => r.CaseId).FirstOrDefaultAsync(ct);
        if (result == null) { await GuardAsync(db, job, ct); await db.Entry(run).ReloadAsync(ct); await CompleteRunAsync(db, run, ct); await FinishAsync(db, job, false, ct); return; }
        if (run.Stale || run.DeadlineUtc <= DateTimeOffset.UtcNow || run.UsedTokens >= run.BudgetTokens) throw new AppException(422, run.Stale ? "stale_sources" : "run_budget_or_deadline", "Run evidence, token budget or deadline prevents further calls.");
        var dataset = await db.EvalDatasets.SingleAsync(d => d.Id == run.DatasetId, ct); var c = await db.EvalCases.SingleAsync(c => c.Id == result.CaseId, ct);
        var ids = JsonDefaults.Read<Guid[]>(c.EvidenceIdsJson); var sources = JsonDefaults.Read<EvidenceSource[]>(dataset.EvidenceJson).Where(s => ids.Contains(s.ChunkId)).ToArray();
        await DatasetService.EvidenceAsync(db, ids, ct);
        var variant = JsonDefaults.Read<VariantDefinition[]>(run.VariantsJson).Single(v => v.Id == result.VariantId);
        var step = result.OutputJson is null ? "candidate" : "judge";
        // Operation IDs are stable across lease reclaim; successful checkpoints prevent duplicate local results.
        var raw = await ai.StepAsync(new(result.Id, step, variant, JsonDocument.Parse(c.InputJson).RootElement.Clone(), sources, run.BudgetTokens - run.UsedTokens, result.OutputJson, step == "judge" ? JsonDocument.Parse(c.ExpectedJson).RootElement.Clone() : null), ct);
        if (raw.Usage.Input < 0 || raw.Usage.Output < 0) throw new AppException(502, "invalid_usage", "Invalid usage.");
        JudgeScore? judge = null; CheckResult? checks = null;
        if (step == "candidate") checks = ScoringRules.Check(raw.OutputJson, c.ExpectedJson, sources);
        else { judge = JsonDefaults.Read<JudgeScore>(raw.OutputJson); if (judge.Score is < 1 or > 5 || judge.RubricVersion != "judge-v1" || string.IsNullOrWhiteSpace(judge.Reason) || judge.Reason.Length > 2000) throw new AppException(502, "invalid_judge", "Judge schema or score invalid."); }
        await GuardAsync(db, job, ct); await db.Entry(run).ReloadAsync(ct);
        if (run.Stale || run.State == "Canceled") throw new AppException(409, "run_changed", "Run canceled or evidence deleted during call.");
        // Always account for the complete successful step before deciding whether further calls fit.
        run.PossibleRepeatedCloudCost |= job.Attempt > 0;
        run.UsedTokens += raw.Usage.Input + raw.Usage.Output; run.Revision++; run.State = step == "candidate" ? "Running" : "Scoring";
        if (step == "candidate") { result.OutputJson = raw.OutputJson; result.CandidateUsageJson = JsonDefaults.Serialize(raw.Usage); result.CandidateLatencyMs = raw.DurationMs; result.ChecksJson = JsonDefaults.Serialize(checks); result.State = "Scoring"; }
        else { result.JudgeJson = JsonDefaults.Serialize(judge); result.JudgeUsageJson = JsonDefaults.Serialize(raw.Usage); result.JudgeLatencyMs = raw.DurationMs; result.State = "Completed"; }
        result.ErrorCode = null; result.Attempts = 0;
        await FinishAsync(db, job, true, ct);
    }
    public static async Task GuardAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        await db.Database.BeginTransactionAsync(ct);
        var rows = await db.IngestionJobs.FromSqlInterpolated($"SELECT * FROM app.\"IngestionJobs\" WHERE \"Id\"={job.Id} AND \"LeaseToken\"={job.LeaseToken} AND \"Status\"='Running' AND \"LeaseUntilUtc\">now() FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        if (rows.Length != 1) throw new AppException(409, "lease_lost", "Lease lost before checkpoint.");
    }
    private static async Task FinishAsync(AppDbContext db, IngestionJob job, bool requeue, CancellationToken ct, int seconds = 0)
    {
        var stored = await db.IngestionJobs.SingleAsync(j => j.Id == job.Id, ct); stored.Status = requeue ? "Queued" : "Succeeded"; stored.LeaseToken = null; stored.LeaseUntilUtc = null; stored.Attempt = 0; stored.ErrorCode = null; stored.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(seconds);
        await db.SaveChangesAsync(ct); await db.Database.CommitTransactionAsync(ct);
    }
    private static async Task CompleteRunAsync(AppDbContext db, EvalRun run, CancellationToken ct)
    {
        var results = await db.EvalCaseResults.Where(r => r.RunId == run.Id).ToArrayAsync(ct);
        run.State = results.All(r => r.State == "Completed") ? "Completed" : results.Any(r => r.State == "Completed") ? "Partial" : "Failed"; run.Revision++; run.CompletedAtUtc = DateTimeOffset.UtcNow;
    }
    private async Task FailAsync(IngestionJob job, Exception ex, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        try { await GuardAsync(db, job, ct); } catch (AppException) { return; }
        var stored = await db.IngestionJobs.SingleAsync(j => j.Id == job.Id, ct);
        var code = ex is AppException a ? a.Code : ex is JsonException ? "invalid_ai_json" : ex is OperationCanceledException ? "operation_timeout" : "job_failed";
        var transient = ex is HttpRequestException or OperationCanceledException || ex is AppException { Status: 503 } && code is not ("foundry_not_configured" or "extractor_not_configured");
        stored.Attempt++; stored.ErrorCode = code; stored.LeaseToken = null; stored.LeaseUntilUtc = null;
        stored.Status = transient && stored.Attempt < 3 ? "RetryWaiting" : "Failed";
        stored.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(Math.Pow(2, stored.Attempt) + Random.Shared.NextDouble(), (ex as AppException)?.RetryAfterSeconds ?? 0));
        if (job.Kind == "Evaluate")
        {
            var run = await db.EvalRuns.SingleAsync(r => r.Id == job.TargetId, ct); run.PossibleRepeatedCloudCost = true; run.Revision++;
            if (stored.Status == "Failed")
            {
                var pending = await db.EvalCaseResults.Where(r => r.RunId == run.Id && r.State != "Completed" && r.State != "Failed").OrderBy(r => r.VariantId).ThenBy(r => r.CaseId).ToArrayAsync(ct);
                bool stop = code is "run_budget_or_deadline" or "ai_budget_exceeded" or "stale_sources" or "foundry_not_configured";
                foreach (var result in stop ? pending : pending.Take(1)) { result.State = "Failed"; result.ErrorCode = code; result.Attempts += stored.Attempt; }
                run.ErrorCode = code;
                if (!stop && pending.Length > 1) { stored.Status = "Queued"; stored.Attempt = 0; }
                else await CompleteRunAsync(db, run, ct);
            }
        }
        else if (stored.Status == "Failed" && job.Kind == "Extract") await db.Documents.Where(d => d.Id == job.DocumentId && d.Status != "Deleting").ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, "Failed").SetProperty(d => d.ErrorCode, code), ct);
        await db.SaveChangesAsync(ct); await db.Database.CommitTransactionAsync(ct);
        logger.LogWarning("Job {Job} failed with {Code}", job.Id, code);
    }
}

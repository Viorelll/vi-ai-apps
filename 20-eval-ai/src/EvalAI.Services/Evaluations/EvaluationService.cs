using System.Globalization;
using EvalAI.Database.Persistence;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using EvalAI.Domain.Services;
using Microsoft.EntityFrameworkCore;
namespace EvalAI.Services.Evaluations;

public sealed class EvaluationService(IDbContextFactory<AppDbContext> factory, VariantCatalog catalog)
{
    public async Task<EvalRun> CreateAsync(CreateRunRequest request, CancellationToken ct)
    {
        var variants = catalog.Resolve(request.VariantIds ?? []);
        if (request.BudgetTokens is < 256 or > 100000) throw new AppException(400, "invalid_budget", "Token budget must be 256-100000.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var dataset = await db.EvalDatasets.SingleOrDefaultAsync(x => x.Id == request.DatasetId, ct) ?? throw new AppException(404, "dataset_not_found", "Dataset not found.");
        var evidence = JsonDefaults.Read<EvidenceSource[]>(dataset.EvidenceJson);
        var current = await DatasetService.EvidenceAsync(db, evidence.Select(s => s.ChunkId).ToArray(), ct);
        if (JsonDefaults.Hash(JsonDefaults.Serialize(current)) != dataset.EvidenceHash) throw new AppException(409, "stale_dataset", "Evidence changed; create a new dataset version.");
        var scope = new EvalScope(dataset.Version, dataset.InputHash, dataset.EvidenceHash, ScoringRules.ChecksVersion, "judge-v1", "safety-v1/" + catalog.Mode);
        if (request.BaselineRunId is { } bid)
        {
            var baseline = await db.EvalRuns.SingleOrDefaultAsync(x => x.Id == bid, ct) ?? throw new AppException(404, "baseline_not_found", "Baseline not found.");
            if (baseline.State != "Completed" || baseline.Stale) throw new AppException(409, "baseline_unavailable", "Baseline must be completed and current.");
            ScoringRules.RequireSameBaseline(scope, JsonDefaults.Read<EvalScope>(baseline.ScopeJson));
            var previous = JsonDefaults.Read<VariantDefinition[]>(baseline.VariantsJson);
            if (variants.Any(v => !previous.Any(p => p.Id == v.Id))) throw new AppException(409, "baseline_variants", "Baseline must contain all selected variants.");
        }
        var rates = (await db.CostRates.ToListAsync(ct)).GroupBy(r => r.DeploymentAlias).Select(g => g.OrderByDescending(r => r.EffectiveAtUtc).First()).ToArray();
        var run = new EvalRun { DatasetId = dataset.Id, BaselineRunId = request.BaselineRunId, ScopeJson = JsonDefaults.Serialize(scope), VariantsJson = JsonDefaults.Serialize(variants), RatesJson = JsonDefaults.Serialize(rates), BudgetTokens = request.BudgetTokens, DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(10) };
        db.Add(run);
        var cases = await db.EvalCases.Where(x => x.DatasetId == dataset.Id).ToArrayAsync(ct);
        foreach (var v in variants) foreach (var c in cases) db.Add(new EvalCaseResult { RunId = run.Id, VariantId = v.Id, CaseId = c.Id });
        db.Add(new IngestionJob { Kind = "Evaluate", TargetId = run.Id });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return run;
    }
    public async Task<object[]> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.EvalRuns.OrderByDescending(x => x.CreatedAtUtc).Take(100).Select(x => new { x.Id, x.DatasetId, x.State, x.Revision, x.Stale, x.CreatedAtUtc }).ToArrayAsync(ct);
    }
    public async Task<object> ReportAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run = await db.EvalRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException(404, "run_not_found", "Run not found.");
        var rows = await db.EvalCaseResults.AsNoTracking().Where(x => x.RunId == id).ToArrayAsync(ct);
        var cases = await db.EvalCases.AsNoTracking().Where(x => x.DatasetId == run.DatasetId).ToArrayAsync(ct);
        var variants = JsonDefaults.Read<VariantDefinition[]>(run.VariantsJson); var rates = JsonDefaults.Read<CostRate[]>(run.RatesJson);
        EvalCaseResult[] baseline = [];
        bool baselineValid = false;
        if (run.BaselineRunId is { } bid)
        {
            var b = await db.EvalRuns.AsNoTracking().SingleAsync(x => x.Id == bid, ct);
            baselineValid = !b.Stale && b.State == "Completed" && !run.Stale && JsonDefaults.Read<EvalScope>(b.ScopeJson) == JsonDefaults.Read<EvalScope>(run.ScopeJson);
            if (baselineValid) baseline = await db.EvalCaseResults.AsNoTracking().Where(x => x.RunId == bid).ToArrayAsync(ct);
        }
        bool Passed(EvalCaseResult r) => r.ChecksJson is not null && JsonDefaults.Read<CheckResult>(r.ChecksJson).Passed;
        object Cost(string? usage)
        {
            if (usage is null) return new { amount = (string?)null, currency = (string?)null, rateVersion = (string?)null, knownPartial = true };
            var u = JsonDefaults.Read<TokenUsage>(usage); var rate = rates.SingleOrDefault(r => r.DeploymentAlias == u.Deployment);
            return new { amount = ScoringRules.KnownTextCost(u, rate)?.ToString(CultureInfo.InvariantCulture), currency = rate?.Currency, rateVersion = rate?.RateVersion, knownPartial = true };
        }
        var summaries = variants.Select(v =>
        {
            var results = rows.Where(r => r.VariantId == v.Id).ToArray(); var latencies = results.Where(r => r.OutputJson != null).Select(r => r.CandidateLatencyMs);
            var before = baseline.Where(r => r.VariantId == v.Id).ToArray();
            return new { variantId = v.Id, variant = v.Name, deployment = v.DeploymentAlias, promptVersion = v.PromptVersion, toolsetVersion = v.ToolsetVersion, total = results.Length, passed = results.Count(Passed), failed = results.Count(r => r.State == "Failed"), deterministicFailed = results.Count(r => r.ChecksJson != null && !Passed(r)), completed = results.Count(r => r.State == "Completed"), judgeMean = results.Where(r => r.JudgeJson != null).Select(r => JsonDefaults.Read<JudgeScore>(r.JudgeJson!).Score).Select(x => (decimal?)x).DefaultIfEmpty(null).Average(), p50Ms = ScoringRules.Percentile(latencies, 0.5m), p95Ms = ScoringRules.Percentile(latencies, 0.95m), failedRate = results.Length == 0 ? 0m : (decimal)results.Count(r => r.State == "Failed") / results.Length, regression = baselineValid && before.Length == results.Length && run.State == "Completed" ? (bool?)(results.Count(Passed) < before.Count(Passed)) : null, checksDelta = baselineValid && run.State == "Completed" ? (int?)(results.Count(Passed) - before.Count(Passed)) : null };
        }).ToArray();
        return new { run.Id, run.DatasetId, run.State, run.Revision, run.ErrorCode, run.Stale, run.BaselineRunId, baselineCompatible = baselineValid, run.BudgetTokens, run.UsedTokens, run.PossibleRepeatedCloudCost, run.CreatedAtUtc, run.CompletedAtUtc, scope = JsonDefaults.Read<EvalScope>(run.ScopeJson), mode = JsonDefaults.Read<EvalScope>(run.ScopeJson).SafetyVersion.Split('/').Last(), costKnownPartial = true, costNote = "Text-token components only; tools, media, cache and repeated calls may add untracked costs. Demo usage is simulated.", progress = new { completed = rows.Count(r => r.State is "Completed" or "Failed"), total = rows.Length }, summaries, results = rows.OrderBy(r => r.VariantId).ThenBy(r => cases.Single(c => c.Id == r.CaseId).Ordinal).Select(r => new { r.Id, r.VariantId, variant = variants.Single(v => v.Id == r.VariantId).Name, r.CaseId, caseName = cases.Single(c => c.Id == r.CaseId).Name, r.State, r.ErrorCode, r.Attempts, output = r.OutputJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<object>(r.OutputJson), checks = r.ChecksJson is null ? null : JsonDefaults.Read<CheckResult>(r.ChecksJson), judge = r.JudgeJson is null ? null : JsonDefaults.Read<JudgeScore>(r.JudgeJson), candidateUsage = r.CandidateUsageJson is null ? null : JsonDefaults.Read<TokenUsage>(r.CandidateUsageJson), judgeUsage = r.JudgeUsageJson is null ? null : JsonDefaults.Read<TokenUsage>(r.JudgeUsageJson), r.CandidateLatencyMs, r.JudgeLatencyMs, candidateCost = Cost(r.CandidateUsageJson), judgeCost = Cost(r.JudgeUsageJson) }) };
    }
    public async Task RetryAsync(Guid id, int revision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var run = await LockedAsync(db, id, ct);
        if (run.Revision != revision) throw new AppException(409, "revision_conflict", "Refresh before retry.");
        if (run.State is not ("Failed" or "Partial") || run.Stale || run.UsedTokens >= run.BudgetTokens || run.DeadlineUtc <= DateTimeOffset.UtcNow) throw new AppException(409, "run_state", "Only a current failed/partial run with remaining budget/deadline can be retried. Otherwise create a new run.");
        var failures = await db.EvalCaseResults.Where(x => x.RunId == id && x.State == "Failed").ToArrayAsync(ct);
        foreach (var r in failures) { r.State = r.OutputJson is null ? "Created" : "Scoring"; r.ErrorCode = null; r.Attempts = 0; }
        run.State = "Created"; run.ErrorCode = null; run.CompletedAtUtc = null; run.Revision++;
        db.Add(new IngestionJob { Kind = "Evaluate", TargetId = id }); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task CancelAsync(Guid id, int revision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var run = await LockedAsync(db, id, ct);
        if (run.Revision != revision) throw new AppException(409, "revision_conflict", "Refresh before cancel.");
        if (run.State is "Completed" or "Canceled" or "Partial" or "Failed") throw new AppException(409, "run_state", "Run already terminal.");
        run.State = "Canceled"; run.Revision++; run.CompletedAtUtc = DateTimeOffset.UtcNow;
        await db.IngestionJobs.Where(j => j.Kind == "Evaluate" && j.TargetId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Canceled").SetProperty(j => j.LeaseToken, (Guid?)null), ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public static async Task<EvalRun> LockedAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        return (await db.EvalRuns.FromSqlInterpolated($"SELECT * FROM app.\"EvalRuns\" WHERE \"Id\"={id} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault() ?? throw new AppException(404, "run_not_found", "Run not found.");
    }
    public async Task<CostRate> AddRateAsync(RateInput r, CancellationToken ct)
    {
        if (!catalog.All.Select(x => x.DeploymentAlias).Append(catalog.JudgeDeployment).Contains(r.DeploymentAlias) || string.IsNullOrWhiteSpace(r.RateVersion) || r.RateVersion.Length > 64 || r.Currency.Length != 3 || !r.Currency.All(char.IsAsciiLetterUpper) || r.InputPerMillion < 0 || r.OutputPerMillion < 0 || r.InputPerMillion > 1000000 || r.OutputPerMillion > 1000000) throw new AppException(400, "invalid_rate", "Supply an allowlisted deployment, version, ISO currency and non-negative rates.");
        await using var db = await factory.CreateDbContextAsync(ct); var rate = new CostRate { DeploymentAlias = r.DeploymentAlias, RateVersion = r.RateVersion, Currency = r.Currency, InputPerMillion = r.InputPerMillion, OutputPerMillion = r.OutputPerMillion }; db.Add(rate);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { throw new AppException(409, "rate_version_exists", "Rate version already exists."); }
        return rate;
    }
}

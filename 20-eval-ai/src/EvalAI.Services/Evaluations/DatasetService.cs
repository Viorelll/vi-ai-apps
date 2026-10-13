using System.Text.Json;
using EvalAI.Database.Persistence;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EvalAI.Services.Evaluations;

public sealed class DatasetService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<object[]> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.EvalDatasets.OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.Name, x.Version, x.InputHash, x.EvidenceHash, x.Revision, caseCount = db.EvalCases.Count(c => c.DatasetId == x.Id) }).ToArrayAsync(ct);
    }
    public async Task<EvalDataset> CreateAsync(CreateDatasetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120 || string.IsNullOrWhiteSpace(request.Version) || request.Version.Length > 64 || request.Cases is null || request.Cases.Length is < 1 or > 100) throw new AppException(400, "invalid_dataset", "Dataset requires name, version and 1-100 cases.");
        foreach (var c in request.Cases) if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 120 || c.Input.ValueKind != JsonValueKind.Object || c.Expected.ValueKind != JsonValueKind.Object || c.Input.GetRawText().Length > 10000 || c.Expected.GetRawText().Length > 10000) throw new AppException(400, "invalid_case", "Each case requires a name and JSON input/expected objects within limits.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var ids = request.Cases.SelectMany(c => c.EvidenceIds ?? []).Distinct().ToArray();
        var sources = await EvidenceAsync(db, ids, ct);
        var canonical = JsonDefaults.Serialize(request.Cases.Select(c => new { c.Name, c.Input, c.Expected, evidenceIds = (c.EvidenceIds ?? []).Order().ToArray() }));
        var dataset = new EvalDataset { Name = request.Name.Trim(), Version = request.Version.Trim(), InputHash = JsonDefaults.Hash(canonical), EvidenceJson = JsonDefaults.Serialize(sources), EvidenceHash = JsonDefaults.Hash(JsonDefaults.Serialize(sources)) };
        db.Add(dataset);
        for (int i = 0; i < request.Cases.Length; i++) { var c = request.Cases[i]; db.Add(new EvalCase { DatasetId = dataset.Id, Ordinal = i, Name = c.Name, InputJson = c.Input.GetRawText(), ExpectedJson = c.Expected.GetRawText(), EvidenceIdsJson = JsonDefaults.Serialize(c.EvidenceIds ?? []) }); }
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { throw new AppException(409, "dataset_version_exists", "Dataset versions are immutable; choose a new version."); }
        return dataset;
    }
    public static async Task<EvidenceSource[]> EvidenceAsync(AppDbContext db, Guid[] ids, CancellationToken ct)
    {
        var sources = await (from c in db.DocumentChunks join d in db.Documents on c.DocumentId equals d.Id where ids.Contains(c.Id) && d.Status == "Ready" && d.ActiveGeneration == c.Generation orderby c.Id select new EvidenceSource(c.Id, d.Id, c.Generation, c.Text, d.FileName, c.PageNumber)).ToArrayAsync(ct);
        if (sources.Length != ids.Distinct().Count()) throw new AppException(409, "evidence_not_ready", "All evidence IDs must refer to ready, active document chunks.");
        if (sources.Length > 200 || sources.Sum(s => s.Text.Length) > 100000) throw new AppException(400, "evidence_limit", "Evidence context exceeds limits.");
        return sources;
    }
    public async Task SeedAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.EvalDatasets.AnyAsync(x => x.Name == "Synthetic arithmetic" && x.Version == "seed-v1", ct)) return;
        JsonElement json(string s) => JsonDocument.Parse(s).RootElement.Clone();
        try { await CreateAsync(new("Synthetic arithmetic", "seed-v1", [new("Two numbers", json("{\"operation\":\"sum\",\"values\":[2,3]}"), json("{\"answer\":5}")), new("Negative numbers", json("{\"operation\":\"sum\",\"values\":[-2,7]}"), json("{\"answer\":5}")), new("Regression probe", json("{\"operation\":\"sum\",\"values\":[10,20],\"demoRegression\":true}"), json("{\"answer\":30}"))]), ct); } catch (AppException e) when (e.Code == "dataset_version_exists") { }
    }
}

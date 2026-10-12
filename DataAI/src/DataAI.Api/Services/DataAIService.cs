using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataAI.Application.Analytics;
using DataAI.Application.Abstractions;
using DataAI.Database.Persistence;
using DataAI.Domain.Contracts;
using DataAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAI.Api.Services;

public sealed class DataAIService(IDbContextFactory<AppDbContext> dbFactory, IAggregatePlanner planner, IObjectStore objectStore) : IDataAIService
{
    public async Task<IReadOnlyList<DatasetSummary>> GetDatasetsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Datasets.OrderBy(x => x.Name).Select(x => new DatasetSummary(x.Id, x.Name, x.Rows.Count, x.Revision, x.Rows.Select(r => r.Currency).FirstOrDefault() ?? "EUR")).ToListAsync(ct);
    }

    public async Task<ImportResult> ImportCsvAsync(string name, Stream csv, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120) throw new ArgumentException("invalid_dataset_name");
        await using var upload = new MemoryStream();
        await csv.CopyToAsync(upload, ct);
        if (upload.Length > 50L * 1024 * 1024) throw new ArgumentException("file_too_large");
        upload.Position = 0;
        using var reader = new StreamReader(upload, Encoding.UTF8, true, 4096, leaveOpen: true);
        var header = ParseCsvLine(await reader.ReadLineAsync(ct) ?? "");
        var expected = new[] { "sold_on", "category", "quantity", "unit_price", "currency" };
        if (!header.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("invalid_csv_schema: expected sold_on,category,quantity,unit_price,currency");
        var rows = new List<SalesRow>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = ParseCsvLine(line);
            if (f.Count != 5 || !DateOnly.TryParseExact(f[0], ["yyyy-MM-dd", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || !decimal.TryParse(f[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity)
                || !decimal.TryParse(f[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var price)) throw new ArgumentException("invalid_csv_row");
            var category = f[1].Trim(); var currency = f[4].Trim().ToUpperInvariant();
            if (category.Length is 0 or > 100 || currency.Length != 3 || quantity < 0 || price < 0) throw new ArgumentException("invalid_csv_values");
            rows.Add(new SalesRow { SoldOn = date, Category = category, Quantity = quantity, UnitPrice = price, Currency = currency });
            if (rows.Count > 100_000) throw new ArgumentException("dataset_row_limit_exceeded");
        }
        if (rows.Count == 0) throw new ArgumentException("dataset_is_empty");
        if (rows.Select(x => x.Currency).Distinct().Count() != 1) throw new ArgumentException("dataset_must_use_one_currency");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var dataset = new Dataset { Name = name.Trim(), Revision = 1, Rows = rows };
        var objectKey = $"datasets/{dataset.Id:N}/source.csv";
        upload.Position = 0;
        await objectStore.PutAsync(objectKey, upload, "text/csv", ct);
        dataset.ObjectKey = objectKey;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Datasets.Add(dataset);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await objectStore.DeleteAsync(objectKey, CancellationToken.None);
            throw;
        }
        return new ImportResult(dataset.Id, dataset.Name, rows.Count, dataset.Revision);
    }

    public async Task<AggregateResult> AnalyzeAsync(AnalyzeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000) throw new ArgumentException("invalid_question");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var dataset = await db.Datasets.Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == request.DatasetId, ct) ?? throw new KeyNotFoundException("dataset_not_found");
        var categories = dataset.Rows.Select(x => x.Category).Distinct().ToArray();
        var spec = await planner.PlanAsync(request.Question, categories, ct);
        var currency = dataset.Rows.Select(x => x.Currency).Distinct().Single();
        spec = spec with { Currency = currency };
        AggregateCalculator.Validate(spec, dataset.Rows);
        var filtered = await db.SalesRows.AsNoTracking().Where(x => x.DatasetId == dataset.Id && x.SoldOn >= spec.From && x.SoldOn < spec.ToExclusive && x.Currency == spec.Currency).ToListAsync(ct);
        var sorted = AggregateCalculator.Calculate(filtered, spec).ToArray();
        var total = sorted.Sum(x => x.Value);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { dataset.Id, dataset.Revision, spec, rows = sorted, total }))).AsSpan()).ToLowerInvariant();
        var explanation = await planner.ExplainAsync(request.Question, spec, sorted, total, ct);
        var run = new AnalysisRun { DatasetId = dataset.Id, Question = request.Question, SpecJson = JsonSerializer.Serialize(spec), RowsJson = JsonSerializer.Serialize(sorted), ResultHash = hash, DatasetRevision = dataset.Revision, Explanation = explanation };
        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return new AggregateResult(run.Id, spec.Metric, spec.GroupBy, spec.Currency, sorted, total, hash, dataset.Revision, explanation, planner.IsConfigured);
    }

    public async Task<AggregateResult?> GetRunAsync(Guid runId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var run = await db.AnalysisRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, ct);
        if (run is null) return null;
        var spec = JsonSerializer.Deserialize<AggregateSpec>(run.SpecJson)!;
        var rows = JsonSerializer.Deserialize<AggregateBucket[]>(run.RowsJson)!;
        return new AggregateResult(run.Id, spec.Metric, spec.GroupBy, spec.Currency, rows, rows.Sum(x => x.Value), run.ResultHash, run.DatasetRevision, run.Explanation, planner.IsConfigured);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else quoted = !quoted; }
            else if (line[i] == ',' && !quoted) { fields.Add(value.ToString().Trim()); value.Clear(); }
            else value.Append(line[i]);
        }
        if (quoted) throw new ArgumentException("invalid_csv_quotes");
        fields.Add(value.ToString().Trim()); return fields;
    }
}

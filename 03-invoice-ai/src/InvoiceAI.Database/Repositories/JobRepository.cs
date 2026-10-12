using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Validation;
namespace InvoiceAI.Database.Repositories;

public sealed class JobRepository(IDbContextFactory<AppDbContext> factory) : IJobRepository
{
    public async Task<IngestionJob?> ClaimAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var token = Guid.NewGuid();
        var jobs = await db.IngestionJobs.FromSqlInterpolated($"""
   UPDATE app."IngestionJobs" SET "Status"='Running', "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '90 seconds'
   WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs" WHERE
    ("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())
    ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
   """).AsNoTracking().ToListAsync(ct);
        return jobs.SingleOrDefault();
    }
    private static async Task<IngestionJob> OwnedAsync(AppDbContext db, IngestionJob j, CancellationToken ct)
    {
        // Lock source first everywhere, then job. Ownership prevents stale workers publishing.
        await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={j.DocumentId} FOR UPDATE").SingleAsync(ct);
        var owned = await db.IngestionJobs.SingleAsync(x => x.Id == j.Id, ct);
        if (owned.LeaseToken != j.LeaseToken || owned.Status != "Running" || owned.LeaseUntilUtc < DateTimeOffset.UtcNow)
            throw new AppException("LEASE_LOST", 409);
        return owned;
    }
    public async Task SchedulePollAsync(IngestionJob j, string operation, int seconds, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owned = await OwnedAsync(db, j, ct);
        owned.ProviderOperation = operation;
        owned.Phase = "Poll";
        owned.PollCount++;
        if (owned.PollCount > 120)
            throw new AppException("EXTRACTION_DEADLINE", 422);
        Requeue(owned, seconds);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task ActivateAsync(IngestionJob j, ExtractionDto result, string rawKey, CancellationToken ct)
    {
        if (result.DocumentId != j.DocumentId || result.Fields.Count > 500 || result.Lines.Count > 200 || result.Fields.Select(x => x.Path).Distinct().Count() != result.Fields.Count)
            throw new AppException("EXTRACTION_CONTRACT_INVALID", 502);
        foreach (var f in result.Fields)
            if (f.RawText.Length > 2000 || f.Confidence is < 0 or > 1 || f.Evidence.Any(e => e.DocumentId != j.DocumentId || e.PageNumber is null or < 1 || e.Text.Length > 2000))
                throw new AppException("EVIDENCE_INVALID", 502);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owned = await OwnedAsync(db, j, ct);
        var i = await db.Invoices.Include(x => x.Lines).Include(x => x.Fields).Include(x => x.Issues).SingleAsync(x => x.Id == j.TargetId, ct);
        var doc = await db.Documents.SingleAsync(x => x.Id == j.DocumentId, ct);
        if (doc.Status is "Deleting" or "Deleted")
            throw new AppException("SOURCE_STALE", 409);
        db.RemoveRange(i.Lines);
        db.RemoveRange(i.Fields);
        db.RemoveRange(i.Issues);
        i.Fields = result.Fields.Select(f => new ExtractedField { InvoiceId = i.Id, Path = f.Path, RawText = f.RawText, ValueJson = f.Value.GetRawText(), Confidence = f.Confidence, EvidenceJson = JsonSerializer.Serialize(f.Evidence) }).ToList();
        i.Lines = result.Lines.Select(l => new InvoiceLine { InvoiceId = i.Id, Ordinal = l.Ordinal, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = l.Amount, Currency = l.Currency, PageNumber = l.PageNumber }).ToList();
        JsonElement V(string path) => result.Fields.FirstOrDefault(x => x.Path == path)?.Value ?? JsonSerializer.SerializeToElement<object?>(null);
        string? Text(string path) => V(path).ValueKind == JsonValueKind.String ? V(path).GetString() : null;
        decimal? Number(string path) => V(path).ValueKind == JsonValueKind.Number && V(path).TryGetDecimal(out var v) ? v : null;
        i.VendorName = Text("vendorName");
        i.InvoiceNumber = Text("invoiceNumber");
        i.Currency = Text("currency");
        i.Date = DateOnly.TryParseExact(Text("date"), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : null;
        i.Subtotal = Number("subtotal");
        i.Tax = Number("tax");
        i.Total = Number("total");
        i.Issues = InvoiceRules.Validate(i).ToList();
        if (i.VendorName is not null && i.InvoiceNumber is not null && await db.Invoices.AnyAsync(x => x.Id != i.Id && x.VendorName == i.VendorName && x.InvoiceNumber == i.InvoiceNumber && x.Date == i.Date && x.Total == i.Total && x.Document.Status == "Ready", ct))
            i.Issues.Add(new()
            {
                InvoiceId = i.Id,
                Code = "POSSIBLE_DUPLICATE",
                Path = "invoiceNumber",
                Severity = "warning"
            });
        db.AddRange(i.Fields);
        db.AddRange(i.Lines);
        db.AddRange(i.Issues);
        i.Revision++;
        i.State = "Extracting";
        i.ErrorCode = null;
        var generation = doc.ActiveGeneration + 1;
        var sources = result.Fields.SelectMany(x => x.Evidence).DistinctBy(x => x.SourceId).ToArray();
        db.DocumentChunks.AddRange(sources.Select((s, n) => new DocumentChunk { DocumentId = doc.Id, Generation = generation, Ordinal = n, Text = s.Text, PageNumber = s.PageNumber }));
        doc.ActiveGeneration = generation;
        doc.Status = "Ready";
        owned.RawObjectKey = rawKey;
        owned.Phase = "Explain";
        owned.Attempt = 0;
        Requeue(owned, 0);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task CompleteAsync(IngestionJob j, ExplanationDto answer, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owned = await OwnedAsync(db, j, ct);
        var i = await db.Invoices.Include(x => x.Fields).Include(x => x.Issues).SingleAsync(x => x.Id == j.TargetId, ct);
        var sources = i.Fields.SelectMany(f => JsonSerializer.Deserialize<List<EvidenceDto>>(f.EvidenceJson)!).Select(x => x.SourceId).ToHashSet();
        if (answer.SourceIds.Any(x => !sources.Contains(x)))
            throw new AppException("CITATION_INVALID", 502);
        var source = await db.Documents.SingleAsync(x => x.Id == j.DocumentId, ct);
        if (source.Status is "Deleting" or "Deleted")
            throw new AppException("SOURCE_STALE", 409);
        source.Status = "Ready";
        i.Explanation = answer.Summary;
        i.SuggestedCategory = answer.SuggestedCategory;
        i.InputTokens += answer.InputTokens;
        i.OutputTokens += answer.OutputTokens;
        i.State = i.Issues.Any(x => x.Severity == "error") ? "NeedsReview" : "Validated";
        i.Revision++;
        i.ErrorCode = null;
        owned.Status = "Completed";
        owned.LeaseToken = null;
        owned.LeaseUntilUtc = null;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task FinishCleanupAsync(IngestionJob j, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owned = await OwnedAsync(db, j, ct);
        var d = await db.Documents.SingleAsync(x => x.Id == j.DocumentId, ct);
        d.Status = "Deleted";
        await db.DocumentChunks.Where(x => x.DocumentId == d.Id).ExecuteDeleteAsync(ct);
        owned.Status = "Completed";
        owned.LeaseToken = null;
        owned.LeaseUntilUtc = null;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task FailAsync(IngestionJob j, string code, bool transient, int delaySeconds, CancellationToken ct, long inputTokens = 0, long outputTokens = 0)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owned = await OwnedAsync(db, j, ct);
        owned.Attempt++;
        owned.ErrorCode = code;
        if (j.Kind == "Analyze" && (inputTokens > 0 || outputTokens > 0))
            await db.Invoices.Where(x => x.Id == j.TargetId).ExecuteUpdateAsync(x => x.SetProperty(i => i.InputTokens, i => i.InputTokens + inputTokens).SetProperty(i => i.OutputTokens, i => i.OutputTokens + outputTokens), ct);
        if (transient && owned.Attempt < 3)
            Requeue(owned, delaySeconds);
        else
        {
            owned.Status = "Failed";
            owned.LeaseToken = null;
            owned.LeaseUntilUtc = null;
            if (j.Kind == "Analyze")
            {
                var i = await db.Invoices.SingleAsync(x => x.Id == j.TargetId, ct);
                i.State = "Failed";
                i.ErrorCode = code;
                i.Revision++;
                var d = await db.Documents.SingleAsync(x => x.Id == j.DocumentId, ct);
                d.Status = "Failed";
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private static void Requeue(IngestionJob j, int seconds)
    {
        j.Status = "RetryWaiting";
        j.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(seconds, 0));
        j.LeaseToken = null;
        j.LeaseUntilUtc = null;
    }
}

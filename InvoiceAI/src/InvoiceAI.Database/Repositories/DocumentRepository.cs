using Microsoft.EntityFrameworkCore;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
namespace InvoiceAI.Database.Repositories;

public sealed class DocumentRepository(IDbContextFactory<AppDbContext> factory) : IDocumentRepository
{
    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().Where(x => x.Status != "Deleting" && x.Status != "Deleted").OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
    }
    public async Task<Document?> FindHashAsync(string hash, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Sha256 == hash && x.Status != "Deleted", ct);
    }
    public async Task<Document> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status != "Deleting" && x.Status != "Deleted", ct) ?? throw new AppException("DOCUMENT_NOT_FOUND", 404);
    }
    public async Task AddAsync(Document document, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);
    }
    public async Task<Guid> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException("DOCUMENT_NOT_FOUND", 404);
        var existing = await db.IngestionJobs.SingleOrDefaultAsync(x => x.TargetId == id && x.Kind == "Cleanup", ct);
        if (existing is not null)
        {
            if (existing.Status == "Failed")
            {
                existing.Status = "Queued";
                existing.Attempt = 0;
                existing.ErrorCode = null;
                existing.LeaseToken = null;
                existing.LeaseUntilUtc = null;
                existing.NextAttemptAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            return existing.Id;
        }
        d.Status = "Deleting";
        await db.Invoices.Where(x => x.DocumentId == id).ExecuteUpdateAsync(x => x.SetProperty(i => i.State, "Stale").SetProperty(i => i.Revision, i => i.Revision + 1), ct);
        await db.IngestionJobs.Where(x => x.DocumentId == id && x.Kind == "Analyze" && x.Status != "Completed").ExecuteUpdateAsync(x => x.SetProperty(j => j.Status, "Cancelled").SetProperty(j => j.LeaseToken, (Guid?)null), ct);
        var job = new IngestionJob { DocumentId = id, TargetId = id, Kind = "Cleanup" };
        db.IngestionJobs.Add(job);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return job.Id;
    }
    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new AppException("DOCUMENT_NOT_FOUND", 404);
        var j = await db.IngestionJobs.SingleOrDefaultAsync(x => x.DocumentId == id && x.Kind == "Analyze" && x.Status == "Failed", ct) ?? throw new AppException("RETRY_STATE_CONFLICT", 409);
        if (d.Status is "Deleting" or "Deleted")
            throw new AppException("DOCUMENT_NOT_FOUND", 404);
        if (j.Phase == "Poll" && j.ErrorCode is "EXTRACTION_FAILED" or "EXTRACTION_DEADLINE")
        {
            j.Phase = "Start";
            j.ProviderOperation = null;
        }
        j.Status = "Queued";
        j.Attempt = 0;
        j.PollCount = 0;
        j.ErrorCode = null;
        j.NextAttemptAtUtc = DateTimeOffset.UtcNow;
        j.LeaseToken = null;
        j.LeaseUntilUtc = null;
        var i = await db.Invoices.SingleAsync(x => x.Id == j.TargetId, ct);
        i.State = "Extracting";
        i.ErrorCode = null;
        i.Revision++;
        d.Status = "Processing";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Database.Persistence;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
namespace InvoiceAI.Database.Repositories;

public sealed class InvoiceRepository(IDbContextFactory<AppDbContext> factory) : IInvoiceRepository
{
    private static IQueryable<Invoice> Graph(AppDbContext db) => db.Invoices.Include(x => x.Document).Include(x => x.Lines).Include(x => x.Fields).Include(x => x.Issues).AsSplitQuery();
    public async Task<IReadOnlyList<Invoice>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await Graph(db).AsNoTracking().Where(x => x.Document.Status != "Deleted" && x.Document.Status != "Deleting").OrderByDescending(x => x.Document.CreatedAtUtc).Take(200).ToListAsync(ct);
    }
    public async Task<Invoice> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await Graph(db).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Document.Status != "Deleted" && x.Document.Status != "Deleting", ct) ?? throw new AppException("INVOICE_NOT_FOUND", 404);
    }
    public async Task<Invoice> CreateAsync(Guid documentId, string kind, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var d = await db.Documents.SingleOrDefaultAsync(x => x.Id == documentId && x.Status != "Deleted" && x.Status != "Deleting", ct) ?? throw new AppException("DOCUMENT_NOT_FOUND", 404);
        var existing = await Graph(db).FirstOrDefaultAsync(x => x.DocumentId == documentId, ct);
        if (existing is not null)
        {
            if (existing.Kind != kind)
                throw new AppException("KIND_CONFLICT", 409);
            return existing;
        }
        var i = new Invoice { DocumentId = d.Id, Document = d, Kind = kind };
        db.Invoices.Add(i);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) { throw new AppException("INVOICE_CONFLICT", 409); }
        return i;
    }
    public async Task SaveAsync(Invoice invoice, long expectedRevision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock the source to serialize corrections with deletion and worker activation.
        var source = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={invoice.DocumentId} FOR UPDATE").SingleAsync(ct);
        if (source.Status != "Ready")
            throw new AppException("SOURCE_STALE", 409);
        var current = await Graph(db).SingleAsync(x => x.Id == invoice.Id, ct);
        if (current.Revision != expectedRevision)
            throw new AppException("REVISION_CONFLICT", 409);
        db.Entry(current).CurrentValues.SetValues(invoice);
        current.Revision = expectedRevision + 1;
        foreach (var l in invoice.Lines)
        {
            var old = current.Lines.Single(x => x.Id == l.Id);
            db.Entry(old).CurrentValues.SetValues(l);
        }
        foreach (var f in invoice.Fields)
        {
            var old = current.Fields.SingleOrDefault(x => x.Id == f.Id);
            if (old is null)
            {
                current.Fields.Add(f);
                db.Add(f);
            }
            else
                db.Entry(old).CurrentValues.SetValues(f);
        }
        db.RemoveRange(current.Issues);
        current.Issues = invoice.Issues;
        db.AddRange(current.Issues);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { throw new AppException("REVISION_CONFLICT", 409); }
        await tx.CommitAsync(ct);
        invoice.Revision = current.Revision;
    }
    public async Task<Guid> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var i = await db.Invoices.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new AppException("INVOICE_NOT_FOUND", 404);
        var doc = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={i.DocumentId} FOR UPDATE").SingleAsync(ct);
        if (doc.Status is "Deleted" or "Deleting")
            throw new AppException("DOCUMENT_NOT_FOUND", 404);
        var job = await db.IngestionJobs.SingleOrDefaultAsync(x => x.TargetId == id && x.Kind == "Analyze" && x.PipelineVersion == "invoice-v1", ct);
        if (job is not null)
            return job.Id;
        job = new()
        {
            DocumentId = i.DocumentId,
            TargetId = id
        };
        db.IngestionJobs.Add(job);
        i.State = "Extracting";
        i.Revision++;
        doc.Status = "Processing";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return job.Id;
    }
    public async Task<bool> HasDuplicateAsync(Invoice i, CancellationToken ct)
    {
        if (i.InvoiceNumber is null || i.VendorName is null)
            return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Invoices.AnyAsync(x => x.Id != i.Id && x.VendorName == i.VendorName && x.InvoiceNumber == i.InvoiceNumber && x.Date == i.Date && x.Total == i.Total && x.Document.Status != "Deleted" && x.Document.Status != "Deleting", ct);
    }
    public async Task<ExportSnapshot> ExportAsync(Invoice invoice, string payload, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var source = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={invoice.DocumentId} FOR UPDATE").SingleAsync(ct);
        var current = await db.Invoices.SingleAsync(x => x.Id == invoice.Id, ct);
        if (source.Status != "Ready" || current.Revision != invoice.Revision || current.State is not ("Validated" or "Exported"))
            throw new AppException("REVISION_CONFLICT", 409);
        var snapshot = await db.ExportSnapshots.SingleOrDefaultAsync(x => x.InvoiceId == invoice.Id && x.Revision == invoice.Revision && x.SchemaVersion == "1", ct);
        if (snapshot is null)
        {
            snapshot = new()
            {
                InvoiceId = invoice.Id,
                Revision = invoice.Revision,
                PayloadJson = payload,
                Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()
            };
            db.ExportSnapshots.Add(snapshot);
        }
        // Export is idempotent; entering Exported does not change the content revision.
        current.State = "Exported";
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return snapshot;
    }
}

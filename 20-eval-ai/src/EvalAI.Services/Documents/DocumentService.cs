using System.Security.Cryptography;
using EvalAI.Database.Persistence;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using EvalAI.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace EvalAI.Services.Documents;

public sealed class DocumentService(IDbContextFactory<AppDbContext> factory, IObjectStore storage)
{
    public async Task<UploadResult> UploadAsync(string name, Stream input, CancellationToken ct)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 240) throw new AppException(400, "invalid_filename", "Invalid file name.");
        var temp = Path.Combine(Path.GetTempPath(), "evalai-" + Guid.NewGuid());
        await using var spool = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0) { if (spool.Length + read > FileValidator.MaxBytes) throw new AppException(413, "file_size_limit", "File exceeds 50 MiB."); await spool.WriteAsync(buffer.AsMemory(0, read), ct); }
        spool.Position = 0; var type = await FileValidator.ValidateAsync(fileName, spool, ct); var hash = Convert.ToHexString(await SHA256.HashDataAsync(spool, ct)); spool.Position = 0;
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.Documents.SingleOrDefaultAsync(d => d.Sha256 == hash && d.Status != "Deleting", ct);
        if (existing != null) return new(existing.Id, null, true);
        var doc = new Document { FileName = fileName, ContentType = type, Sha256 = hash, SizeBytes = spool.Length }; doc.ObjectKey = "originals/" + doc.Id;
        var job = new IngestionJob { DocumentId = doc.Id }; await storage.EnsureBucketAsync(ct);
        try { await storage.PutAsync(doc.ObjectKey, spool, type, ct); db.Add(doc); db.Add(job); await db.SaveChangesAsync(ct); }
        catch (Exception ex)
        {
            try { await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None); } catch {/* Original exception retained; cleanup reconciliation can remove orphan. */}
            if (ex is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }) { db.ChangeTracker.Clear(); existing = await db.Documents.SingleAsync(d => d.Sha256 == hash && d.Status != "Deleting", ct); return new(existing.Id, null, true); }
            throw;
        }
        return new(doc.Id, job.Id, false);
    }
    public async Task<Document> GetAsync(Guid id, CancellationToken ct) { await using var db = await factory.CreateDbContextAsync(ct); return await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status != "Deleting", ct) ?? throw new AppException(404, "document_not_found", "Document not found."); }
    public async Task<object[]> ListAsync(CancellationToken ct) { await using var db = await factory.CreateDbContextAsync(ct); return await db.Documents.Where(x => x.Status != "Deleting").OrderByDescending(x => x.CreatedAtUtc).Select(x => new { x.Id, x.FileName, x.Status, x.ErrorCode, x.ActiveGeneration }).ToArrayAsync(ct); }
    public async Task<object[]> ChunksAsync(Guid id, CancellationToken ct) { var d = await GetAsync(id, ct); await using var db = await factory.CreateDbContextAsync(ct); return await db.DocumentChunks.Where(x => x.DocumentId == id && x.Generation == d.ActiveGeneration && d.Status == "Ready").OrderBy(x => x.Ordinal).Select(x => new { x.Id, x.Text, x.PageNumber, x.Generation, contentUrl = "/api/documents/" + id + "/content" }).ToArrayAsync(ct); }
    public async Task<Guid> RetryAsync(Guid id, CancellationToken ct) { await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct); var d = (await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={id} FOR UPDATE").ToArrayAsync(ct)).SingleOrDefault() ?? throw new AppException(404, "document_not_found", "Document not found."); if (d.Status != "Failed") throw new AppException(409, "document_state", "Only failed extraction can be retried."); d.Status = "Uploaded"; d.ErrorCode = null; var job = new IngestionJob { DocumentId = id }; db.Add(job); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return job.Id; }
    public async Task<Guid> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var d = await db.Documents.SingleOrDefaultAsync(x => x.Id == id && x.Status != "Deleting", ct) ?? throw new AppException(404, "document_not_found", "Document not found.");
        d.Status = "Deleting";
        var datasets = await db.EvalDatasets.ToArrayAsync(ct); var ids = datasets.Where(x => JsonDefaults.Read<EvidenceSource[]>(x.EvidenceJson).Any(s => s.DocumentId == id)).Select(x => x.Id).ToArray();
        await db.EvalRuns.Where(r => ids.Contains(r.DatasetId)).ExecuteUpdateAsync(s => s.SetProperty(r => r.Stale, true).SetProperty(r => r.Revision, r => r.Revision + 1), ct);
        await db.IngestionJobs.Where(j => j.DocumentId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, "Canceled").SetProperty(j => j.LeaseToken, (Guid?)null), ct);
        var job = new IngestionJob { DocumentId = id, Kind = "Cleanup" }; db.Add(job); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return job.Id;
    }
}

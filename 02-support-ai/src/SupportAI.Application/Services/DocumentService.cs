using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SupportAI.Application.Abstractions;
using SupportAI.Database.Persistence;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
using SupportAI.Domain.Models;
namespace SupportAI.Application.Services;

public sealed class DocumentService(IDbContextFactory<AppDbContext> factory, IObjectStore storage)
{
    public async Task<UploadResult> UploadAsync(string name, Stream source, CancellationToken ct, bool policy = false)
    {
        name = Path.GetFileName(name.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240)
            throw new SupportException("invalid_file_name");
        var temp = Path.GetTempFileName();
        try
        {
            await using var spool = new FileStream(temp, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous);
            var buffer = new byte[65536];
            long length = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                length += read;
                if (length > FileValidator.MaxBytes)
                    throw new SupportException("file_too_large", 413);
                await spool.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (length == 0)
                throw new SupportException("empty_file");
            spool.Position = 0;
            var header = new byte[(int)Math.Min(length, 512)];
            await spool.ReadExactlyAsync(header, ct);
            var mime = FileValidator.Validate(name, header);
            spool.Position = 0;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(spool, ct));
            await using var db = await factory.CreateDbContextAsync(ct);
            var existing = await db.Documents.SingleOrDefaultAsync(x => x.Sha256 == hash, ct);
            if (existing is not null)
            {
                if (existing.Status == "Deleting")
                    throw new SupportException("document_deleting", 409);
                return new(existing.Id, null, existing.Status, true);
            }
            var doc = new Document { FileName = name, ContentType = mime, Sha256 = hash, ObjectKey = $"originals/{Guid.NewGuid():N}", SizeBytes = length, IsPolicy = policy };
            var job = new IngestionJob { Kind = "Extract", DocumentId = doc.Id };
            await storage.EnsureBucketAsync(ct);
            spool.Position = 0;
            await storage.PutAsync(doc.ObjectKey, spool, mime, ct);
            db.Add(doc);
            db.Add(job);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None);
                await using var check = await factory.CreateDbContextAsync(ct);
                var raced = await check.Documents.SingleOrDefaultAsync(x => x.Sha256 == hash, ct);
                if (raced is not null)
                    return new(raced.Id, null, raced.Status, true);
                throw;
            }
            return new(doc.Id, job.Id, doc.Status, false);
        }
        finally { File.Delete(temp); }
    }
    public async Task<Document> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status != "Deleting", ct) ?? throw new SupportException("document_not_found", 404);
    }
    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Documents.AsNoTracking().Where(x => x.Status != "Deleting").OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
    }
    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("document_not_found", 404);
        if (doc.Status != "Failed")
            throw new SupportException("document_not_failed", 409);
        doc.Status = "Uploaded";
        doc.ErrorCode = null;
        db.Add(new IngestionJob { Kind = "Extract", DocumentId = id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("document_not_found", 404);
        if (doc.IsPolicy)
            throw new SupportException("seed_policy_protected", 409);
        if (doc.Status == "Deleting")
            return;
        doc.Status = "Deleting";
        await db.IngestionJobs.Where(x => x.DocumentId == id && x.Status != "Complete").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Failed").SetProperty(x => x.LeaseToken, (Guid?)null), ct);
        var ids = await db.Citations.Where(x => x.DocumentId == id).Select(x => x.MessageId).ToArrayAsync(ct);
        await db.Messages.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.Stale, true), ct);
        var related = await db.ActionProposals.Where(x => ids.Contains(x.MessageId) && x.State == "Draft").ToListAsync(ct);
        foreach (var p in related)
        {
            p.State = "Expired";
            p.Revision++;
        }
        db.Add(new IngestionJob { Kind = "Cleanup", DocumentId = id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

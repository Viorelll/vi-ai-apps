using System.Security.Cryptography;
using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Api.Services;

public sealed class DocumentService(AppDbContext db, IObjectStore storage)
{
    public async Task<(Document Document, bool Existing)> UploadAsync(
        IFormFile file,
        CancellationToken ct
    )
    {
        if (file.Length is <= 0 or > UploadValidator.MaxBytes)
            throw new DomainException("file_size_limit", 413);
        string tmp = Path.GetTempFileName();
        string? key = null;
        try
        {
            await using var stream = new FileStream(
                tmp,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous
            );
            await using var input = file.OpenReadStream();
            byte[] buffer = new byte[81920];
            long size = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                size += count;
                if (size > UploadValidator.MaxBytes)
                    throw new DomainException("file_size_limit", 413);
                await stream.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            stream.Position = 0;
            byte[] magic = new byte[Math.Min(512, size)];
            await stream.ReadExactlyAsync(magic, ct);
            string name = Path.GetFileName(file.FileName);
            if (name.Length is < 1 or > 240)
                throw new DomainException("invalid_filename");
            string mime = UploadValidator.Detect(magic, name);
            stream.Position = 0;
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            var existing = await db.Documents.SingleOrDefaultAsync(
                x => x.Sha256 == hash && x.Status != "Deleted",
                ct
            );
            if (existing != null)
            {
                if (existing.Status == "Deleting")
                    throw new DomainException("document_deleting", 409);
                return (existing, true);
            }
            var doc = new Document
            {
                FileName = name,
                ContentType = mime,
                Sha256 = hash,
                SizeBytes = size,
                ObjectKey = $"originals/{Guid.NewGuid()}/{hash}",
            };
            key = doc.ObjectKey;
            stream.Position = 0;
            await storage.PutAsync(key, stream, mime, ct);
            db.Documents.Add(doc);
            db.Jobs.Add(new IngestionJob { DocumentId = doc.Id });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var winner = await db.Documents.SingleOrDefaultAsync(
                    x => x.Sha256 == hash && x.Status != "Deleted",
                    ct
                );
                if (winner == null)
                    throw;
                await storage.DeleteAsync(key, ct);
                key = null;
                return (winner, true);
            }
            key = null;
            return (doc, false);
        }
        finally
        {
            File.Delete(tmp);
            if (key != null)
                await storage.DeleteAsync(key, CancellationToken.None);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var d =
            await db.Documents.FindAsync([id], ct)
            ?? throw new DomainException("document_not_found", 404);
        d.Status = "Deleting";
        var plans = await db
            .Plans.Where(p => p.Sources.Any(s => s.DocumentId == id))
            .ToListAsync(ct);
        foreach (var p in plans)
        {
            p.Revision++;
            p.State = "NeedsReview";
        }
        db.Jobs.Add(new IngestionJob { DocumentId = id, Kind = "Cleanup" });
        await db.SaveChangesAsync(ct);
    }

    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        var d =
            await db.Documents.FindAsync([id], ct)
            ?? throw new DomainException("document_not_found", 404);
        if (d.Status != "Failed")
            throw new DomainException("document_not_failed", 409);
        d.Status = "Uploaded";
        d.ErrorCode = null;
        db.Jobs.Add(new IngestionJob { DocumentId = id });
        await db.SaveChangesAsync(ct);
    }
}

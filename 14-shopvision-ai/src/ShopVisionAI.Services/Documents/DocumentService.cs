using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ShopVisionAI.Database;
using ShopVisionAI.Domain.Entities;
using ShopVisionAI.Infrastructure.Storage;
using ShopVisionAI.Infrastructure.Validation;
using ShopVisionAI.Services.Errors;

namespace ShopVisionAI.Services.Documents;

public sealed class DocumentService(AppDbContext db, ObjectStore store)
{
    public async Task<(Document Document, bool Existing)> UploadAsync(
        Stream input,
        string fileName,
        long length,
        CancellationToken ct
    )
    {
        var tmp = Path.GetTempFileName();
        string? key = null;
        try
        {
            await using var spool = new FileStream(
                tmp,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous
            );
            var buffer = new byte[81920];
            long size = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                size += count;
                if (size > ImageValidator.MaxBytes)
                    throw new DomainException("invalid_file_size", 413);
                await spool.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            spool.Position = 0;
            var header = new byte[12];
            var read = await spool.ReadAsync(header, ct);
            var mime = ImageValidator.Detect(header.AsSpan(0, read), fileName, size);
            spool.Position = 0;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(spool, ct));
            var existing = await db.Documents.FirstOrDefaultAsync(
                x => x.Sha256 == hash && x.Status != "Deleted",
                ct
            );
            if (existing != null)
                return (existing, true);
            var doc = new Document
            {
                Sha256 = hash,
                ObjectKey = "originals/" + Guid.NewGuid(),
                FileName = Path.GetFileName(fileName),
                Mime = mime,
                Size = size,
            };
            key = doc.ObjectKey;
            spool.Position = 0;
            await store.PutAsync(key, spool, mime, ct);
            db.Documents.Add(doc);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var winner = await db.Documents.FirstOrDefaultAsync(x => x.Sha256 == hash, ct);
                if (winner is null)
                    throw;
                await store.DeleteAsync(key, ct);
                key = null;
                return (winner, true);
            }
            key = null;
            return (doc, false);
        }
        finally
        {
            if (key != null)
                await store.DeleteAsync(key, CancellationToken.None);
            File.Delete(tmp);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([id], ct)
            ?? throw new DomainException("document_not_found", 404);
        doc.Status = "Deleted";
        doc.Sha256 = "deleted:" + doc.Id;
        var searches = await db.VisualSearches.Where(x => x.ImageDocumentId == id).ToListAsync(ct);
        foreach (var search in searches)
        {
            search.State = "Stale";
            search.Revision++;
        }
        await db.SaveChangesAsync(ct);
        await store.DeleteAsync(doc.ObjectKey, ct);
    }
}

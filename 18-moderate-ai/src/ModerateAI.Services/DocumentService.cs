using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ModerateAI.Database;
using ModerateAI.Domain.Entities;
using ModerateAI.Domain.Interfaces;
using ModerateAI.Domain.Models;

namespace ModerateAI.Services;

public sealed class DocumentService(IDbContextFactory<AppDbContext> factory, IObjectStore store)
{
    public async Task<(Document Document, bool Existing)> UploadAsync(
        Stream stream,
        string fileName,
        CancellationToken ct
    )
    {
        fileName = Path.GetFileName(fileName);
        if (fileName.Length is 0 or > 240)
            throw new DomainException(400, "invalid_filename");
        // Images are bounded to the active Content Safety adapter's 4 MiB limit.
        using var buffer = new MemoryStream();
        var block = new byte[65536];
        int read;
        while ((read = await stream.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + read > ImageValidator.MaximumBytes)
                throw new DomainException(413, "image_size_limit_4_mib");
            await buffer.WriteAsync(block.AsMemory(0, read), ct);
        }
        byte[] bytes = buffer.ToArray();
        string mime = ImageValidator.Validate(bytes, fileName),
            hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await using var db = await factory.CreateDbContextAsync(ct);
        var prior = await db.Documents.SingleOrDefaultAsync(
            x => x.Sha256 == hash && x.Status != "Deleted",
            ct
        );
        if (prior != null)
        {
            if (prior.Status != "Ready")
                throw new DomainException(409, "document_deleted");
            return (prior, true);
        }
        var doc = new Document
        {
            FileName = fileName,
            ContentType = mime,
            Sha256 = hash,
            ObjectKey = "originals/" + Guid.NewGuid(),
            SizeBytes = bytes.Length,
        };
        buffer.Position = 0;
        await store.PutAsync(doc.ObjectKey, buffer, mime, ct);
        db.Add(doc);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
            when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            await store.DeleteAsync(doc.ObjectKey, ct);
            db.ChangeTracker.Clear();
            prior = await db.Documents.SingleAsync(
                x => x.Sha256 == hash && x.Status != "Deleted",
                ct
            );
            if (prior.Status != "Ready")
                throw new DomainException(409, "document_deleted");
            return (prior, true);
        }
        catch
        {
            await store.DeleteAsync(doc.ObjectKey, CancellationToken.None);
            throw;
        }
        return (doc, false);
    }

    public async Task<Document> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db
                .Documents.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.Status == "Ready", ct)
            ?? throw new DomainException(404, "document_not_found");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doc =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException(404, "document_not_found");
        if (doc.Status != "Ready")
            return;
        doc.Status = "Deleting";
        doc.Revision++;
        // Fence any in-flight result/review through the item's concurrency token.
        await db
            .ModerationItems.Where(x => x.ImageDocumentId == id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Stale, true)
                        .SetProperty(x => x.Revision, x => x.Revision + 1),
                ct
            );
        db.Add(new IngestionJob { DocumentId = id, Kind = "Cleanup" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

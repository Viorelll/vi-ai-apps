using System.Security.Cryptography;
using ImageAI.Contracts.Dto;
using ImageAI.Database;
using ImageAI.Domain.Entities;
using ImageAI.Domain.Models;
using ImageAI.Infrastructure.Storage;
using ImageAI.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ImageAI.Services.Documents;

public sealed class DocumentService(AppDbContext db, ObjectStore store)
{
    public async Task<DocumentDto> UploadAsync(string name, byte[] bytes, CancellationToken ct)
    {
        var info = ImageValidator.Inspect(bytes);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await db.Documents.SingleOrDefaultAsync(x => x.Sha256 == hash, ct);
        if (existing is not null)
            return Map(existing);
        var d = new Document
        {
            FileName = Path.GetFileName(name)[..Math.Min(Path.GetFileName(name).Length, 200)],
            Sha256 = hash,
            SizeBytes = bytes.Length,
            Width = info.Width,
            Height = info.Height,
            HasAlpha = info.HasAlpha,
            ContentType = info.ContentType,
            ObjectKey = $"documents/{Guid.NewGuid():N}",
        };
        await store.PutAsync(d.ObjectKey, bytes, d.ContentType, ct);
        try
        {
            db.Documents.Add(d);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
            when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.Entry(d).State = EntityState.Detached;
            await store.DeleteAsync(d.ObjectKey, ct);
            return Map(await db.Documents.SingleAsync(x => x.Sha256 == hash, ct));
        }
        catch
        {
            await store.DeleteAsync(d.ObjectKey, CancellationToken.None);
            throw;
        }
        return Map(d);
    }

    public async Task<Document> FindAsync(Guid id, CancellationToken ct) =>
        await db.Documents.FindAsync([id], ct)
        ?? throw new DomainException("document_not_found", 404);

    public static DocumentDto Map(Document d) =>
        new(
            d.Id,
            d.FileName,
            d.ContentType,
            d.Sha256,
            d.SizeBytes,
            d.Width,
            d.Height,
            d.HasAlpha,
            d.Status
        );

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var d = await FindAsync(id, ct);
        if (await db.Runs.AnyAsync(x => x.InputDocumentId == id || x.MaskDocumentId == id, ct))
            throw new DomainException("document_used_by_immutable_run", 409);
        db.Documents.Remove(d);
        await db.SaveChangesAsync(ct);
        await store.DeleteAsync(d.ObjectKey, ct);
    }
}

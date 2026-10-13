using System.Security.Cryptography;
using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Domain.Entities;
using AccessReadAI.Infrastructure.Storage;
using AccessReadAI.Services.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Services.Documents;

public sealed class DocumentService(AppDbContext db, IObjectStore storage)
{
    public async Task<(Document Document, bool Existing)> UploadAsync(
        IFormFile file,
        CancellationToken ct
    )
    {
        if (file.Length is <= 0 or > FileValidator.MaxBytes)
            throw new ApiFault(413, "file_size_limit");
        var temp = Path.GetTempFileName();
        try
        {
            await using var spool = new FileStream(
                temp,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous
            );
            await using var input = file.OpenReadStream();
            byte[] buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += count;
                if (total > FileValidator.MaxBytes)
                    throw new ApiFault(413, "file_size_limit");
                await spool.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            spool.Position = 0;
            byte[] prefix = new byte[(int)Math.Min(512, total)];
            await spool.ReadExactlyAsync(prefix, ct);
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (name.Length is 0 or > 240)
                throw new ApiFault(400, "invalid_filename");
            var type = FileValidator.Detect(name, prefix);
            spool.Position = 0;
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(spool, ct));
            var existing = await db.Documents.SingleOrDefaultAsync(x => x.Sha256 == hash, ct);
            if (existing != null)
            {
                if (existing.State == "Deleting")
                    throw new ApiFault(409, "document_deleting");
                return (existing, true);
            }
            var doc = new Document
            {
                FileName = name,
                ContentType = type,
                Sha256 = hash,
                ObjectKey = $"originals/{Guid.NewGuid():N}",
                SizeBytes = total,
            };
            spool.Position = 0;
            await storage.PutAsync(doc.ObjectKey, spool, type, ct);
            db.Documents.Add(doc);
            db.Jobs.Add(new IngestionJob { DocumentId = doc.Id });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e)
                when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                db.ChangeTracker.Clear();
                await storage.DeleteAsync(doc.ObjectKey, ct);
                var winner = await db.Documents.SingleAsync(x => x.Sha256 == hash, ct);
                return (winner, true);
            }
            catch
            {
                await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None);
                throw;
            }
            return (doc, false);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([id], ct) ?? throw new ApiFault(404, "document_not_found");
        if (doc.State != "Failed")
            throw new ApiFault(409, "document_not_failed");
        doc.State = "Uploaded";
        doc.ErrorCode = null;
        db.Jobs.Add(new IngestionJob { DocumentId = id });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([id], ct) ?? throw new ApiFault(404, "document_not_found");
        if (doc.State == "Deleting")
            return;
        doc.State = "Deleting";
        await db
            .Readings.Where(x => x.DocumentId == id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.State, "Stale")
                        .SetProperty(x => x.Revision, x => x.Revision + 1),
                ct
            );
        await db
            .Jobs.Where(x => x.DocumentId == id && x.State != "Completed")
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.State, "Cancelled")
                        .SetProperty(x => x.LeaseToken, (Guid?)null),
                ct
            );
        db.Jobs.Add(new IngestionJob { DocumentId = id, Kind = "Cleanup" });
        await db.SaveChangesAsync(ct);
    }
}

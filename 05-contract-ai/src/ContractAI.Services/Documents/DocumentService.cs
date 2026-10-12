using System.Security.Cryptography;
using ContractAI.Database.Persistence;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Entities;
using ContractAI.Domain.Models;
using ContractAI.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContractAI.Services.Documents;

public sealed class DocumentService(IDbContextFactory<AppDbContext> factory, IObjectStore storage)
{
    public async Task<UploadResult> UploadAsync(string name, Stream input, CancellationToken ct)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 240)
            throw new AppException(400, "invalid_filename", "Invalid file name.");
        var temp = Path.Combine(Path.GetTempPath(), "contractai-" + Guid.NewGuid());
        try
        {
            await using var spool = new FileStream(
                temp,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose
            );
            var buffer = new byte[81920];
            int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (spool.Length + count > FileValidator.MaxBytes)
                    throw new AppException(413, "file_size_limit", "File exceeds 50 MiB.");
                await spool.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            spool.Position = 0;
            var type = await FileValidator.ValidateAsync(fileName, spool, ct);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(spool, ct));
            spool.Position = 0;
            await using var db = await factory.CreateDbContextAsync(ct);
            var existing = await db.Documents.SingleOrDefaultAsync(
                x => x.Sha256 == hash && x.Status != "Deleting",
                ct
            );
            if (existing is not null)
            {
                if (existing.Status == "Deleting")
                    throw new AppException(
                        409,
                        "document_deleted",
                        "This document has been deleted."
                    );
                return new(existing.Id, null, true);
            }
            var document = new Document
            {
                FileName = fileName,
                ContentType = type,
                Sha256 = hash,
                SizeBytes = spool.Length,
            };
            document.ObjectKey = "originals/" + document.Id;
            var job = new IngestionJob { DocumentId = document.Id };
            await storage.EnsureBucketAsync(ct);
            await storage.PutAsync(document.ObjectKey, spool, type, ct);
            try
            {
                db.Add(document);
                db.Add(job);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                await storage.DeleteAsync(document.ObjectKey, CancellationToken.None);
                if (
                    ex is DbUpdateException
                    {
                        InnerException: PostgresException { SqlState: "23505" }
                    }
                )
                {
                    db.ChangeTracker.Clear();
                    existing = await db.Documents.SingleAsync(
                        x => x.Sha256 == hash && x.Status != "Deleting",
                        ct
                    );
                    return new(existing.Id, null, true);
                }
                throw;
            }
            return new(document.Id, job.Id, false);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    public async Task<Document> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db
                .Documents.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.Status != "Deleting", ct)
            ?? throw new AppException(404, "document_not_found", "Document not found.");
    }

    public async Task<Guid> RetryAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var doc =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "document_not_found", "Document not found.");
        if (doc.Status != "Failed")
            throw new AppException(
                409,
                "document_state",
                "Only a failed extraction can be retried."
            );
        doc.Status = "Uploaded";
        doc.ErrorCode = null;
        var job = new IngestionJob { DocumentId = id };
        db.Add(job);
        await db.SaveChangesAsync(ct);
        return job.Id;
    }

    public async Task<Guid> DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var doc =
            await db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "document_not_found", "Document not found.");
        doc.Status = "Deleting";
        var versions = await db.ContractVersions.Where(x => x.DocumentId == id).ToListAsync(ct);
        foreach (var version in versions)
        {
            version.State = "Stale";
            version.Revision++;
        }
        var ids = versions.Select(x => x.Id).ToArray();
        await db
            .Comparisons.Where(x => ids.Contains(x.LeftVersionId) || ids.Contains(x.RightVersionId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "Stale"), ct);
        await db
            .IngestionJobs.Where(x => x.DocumentId == id || ids.Contains(x.TargetId ?? Guid.Empty))
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.Status, "Canceled")
                        .SetProperty(x => x.LeaseToken, (Guid?)null),
                ct
            );
        var job = new IngestionJob { DocumentId = id, Kind = "Cleanup" };
        db.Add(job);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return job.Id;
    }
}

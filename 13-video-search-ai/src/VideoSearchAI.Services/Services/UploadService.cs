using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Infrastructure.Storage;
using VideoSearchAI.Services.Options;

namespace VideoSearchAI.Services.Services;

public sealed class UploadService(
    IDbContextFactory<AppDbContext> factory,
    ObjectStore store,
    MediaProbe probe,
    IOptions<PipelineOptions> options
)
{
    public async Task<UploadDto> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > options.Value.MaxUploadBytes)
            throw new DomainException("file_size_limit", 413);
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".mp4" and not ".webm")
            throw new DomainException("unsupported_video", 415);
        var temporary = Path.Combine(
            Path.GetTempPath(),
            $"videosearch-{Guid.NewGuid():N}{extension}"
        );
        try
        {
            await using var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                65536,
                FileOptions.Asynchronous
            );
            await using var input = file.OpenReadStream();
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > options.Value.MaxUploadBytes)
                    throw new DomainException("file_size_limit", 413);
                await stream.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (total == 0)
                throw new DomainException("empty_file");
            stream.Position = 0;
            var signature = new byte[12];
            await stream.ReadExactlyAsync(signature, ct);
            var mp4 = System.Text.Encoding.ASCII.GetString(signature, 4, 4) == "ftyp";
            var webm =
                signature[0] == 0x1a
                && signature[1] == 0x45
                && signature[2] == 0xdf
                && signature[3] == 0xa3;
            if ((extension == ".mp4" && !mp4) || (extension == ".webm" && !webm))
                throw new DomainException("mime_mismatch", 415);
            await stream.FlushAsync(ct);
            var duration = await probe.DurationAsync(temporary, ct);
            stream.Position = 0;
            var hash = Convert
                .ToHexString(await SHA256.HashDataAsync(stream, ct))
                .ToLowerInvariant();
            await using var db = await factory.CreateDbContextAsync(ct);
            var existing = await db.Documents.SingleOrDefaultAsync(
                d => d.Sha256 == hash && !d.Deleted,
                ct
            );
            if (existing != null)
            {
                if (existing.Deleted)
                    throw new DomainException("document_deleted", 409);
                return new(existing.Id, true);
            }
            var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (name.Length > 240)
                name = name[^240..];
            var document = new Document
            {
                FileName = name,
                ContentType = mp4 ? "video/mp4" : "video/webm",
                SizeBytes = total,
                DurationMs = duration,
                Sha256 = hash,
            };
            document.ObjectKey = $"originals/{document.Id:N}/{extension.TrimStart('.')}";
            stream.Position = 0;
            await store.PutAsync(document.ObjectKey, stream, document.ContentType, ct);
            db.Documents.Add(document);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e)
                when (e.InnerException
                        is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }
                )
            {
                await store.DeleteAsync(document.ObjectKey, ct);
                db.ChangeTracker.Clear();
                existing = await db.Documents.SingleAsync(d => d.Sha256 == hash && !d.Deleted, ct);
                if (existing.Deleted)
                    throw new DomainException("document_deleted", 409);
                return new(existing.Id, true);
            }
            catch
            {
                try
                {
                    await store.DeleteAsync(document.ObjectKey, CancellationToken.None);
                }
                catch
                { /* Reconcile orphaned objects before deleting storage volumes. */
                }
                throw;
            }
            return new(document.Id, false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}

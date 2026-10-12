using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using ProcureAI.Application.Abstractions;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.Entities;

namespace ProcureAI.Application.Services;

public sealed class DocumentService(IProcurementRepository repository, IObjectStore objects)
{
    public const long MaxBytes = 50L * 1024 * 1024;
    public async Task<(Document Document, bool Existing)> UploadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0) throw new AppException("empty_file");
        if (file.Length > MaxBytes) throw new AppException("file_too_large", 413);
        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (fileName.Length is 0 or > 240) throw new AppException("invalid_filename");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => throw new AppException("unsupported_file_type", 415)
        };
        var temp = Path.Combine(Path.GetTempPath(), $"procureai-{Guid.NewGuid():N}.upload");
        try
        {
            await using var spool = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await using var input = file.OpenReadStream();
            var buffer = new byte[81920]; int read; long length = 0;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                length += read;
                if (length > MaxBytes) throw new AppException("file_too_large", 413);
                await spool.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (length == 0) throw new AppException("empty_file");
            spool.Position = 0;
            var header = new byte[(int)Math.Min(4096, length)];
            await spool.ReadExactlyAsync(header, ct);
            var valid = extension switch
            {
                ".pdf" => header.AsSpan().StartsWith("%PDF-"u8),
                ".png" => header.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".jpg" or ".jpeg" => header.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
                ".docx" => header.AsSpan().StartsWith(new byte[] { 80, 75, 3, 4 }),
                _ => !header.Contains((byte)0)
            };
            if (!valid) throw new AppException("file_signature_mismatch", 415);
            if (extension is ".txt" or ".md" or ".json")
            {
                spool.Position = 0;
                using var reader = new StreamReader(spool, new UTF8Encoding(false, true), true, 81920, leaveOpen: true);
                var text = await reader.ReadToEndAsync(ct);
                if (text.Length > 200000) throw new AppException("text_limit", 413);
                if (extension == ".json")
                {
                    try { using var json = System.Text.Json.JsonDocument.Parse(text); }
                    catch (System.Text.Json.JsonException) { throw new AppException("invalid_json"); }
                }
            }
            spool.Position = 0;
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(spool, ct));
            var existing = await repository.FindDocumentAsync(hash, ct);
            if (existing is not null)
            {
                if (existing.State == "Deleting") throw new AppException("document_deleted", 409);
                return (existing, true);
            }
            var id = Guid.NewGuid();
            var key = $"originals/{id}/{hash}{extension}";
            await objects.EnsureBucketAsync(ct); spool.Position = 0;
            await objects.PutAsync(key, spool, contentType, ct);
            Document saved;
            try
            {
                saved = await repository.AddDocumentAsync(new Document
                {
                    Id = id,
                    FileName = fileName,
                    ContentType = contentType,
                    Sha256 = hash,
                    ObjectKey = key,
                    SizeBytes = length
                }, ct);
            }
            catch
            {
                try { await objects.DeleteAsync(key, CancellationToken.None); } catch { /* Orphan reconciled by storage lifecycle. */ }
                throw;
            }
            if (saved.Id != id) await objects.DeleteAsync(key, ct);
            return (saved, saved.Id != id);
        }
        catch (DecoderFallbackException) { throw new AppException("invalid_utf8"); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

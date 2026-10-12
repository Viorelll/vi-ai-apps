using System.Security.Cryptography;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Domain.Entities;
using InvoiceAI.Services.Abstractions;
using InvoiceAI.Services.Exceptions;
using InvoiceAI.Services.Validation;
namespace InvoiceAI.Services.Services;

public sealed class DocumentService(IDocumentRepository repository, IObjectStore storage)
{
    public async Task<UploadResponse> UploadAsync(Stream input, string fileName, string contentType, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"invoiceai-{Guid.NewGuid():N}");
        try
        {
            await using var spool = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.Asynchronous);
            var buffer = new byte[65536];
            long size = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > FileValidator.MaxBytes)
                    throw new AppException("FILE_TOO_LARGE", 413);
                await spool.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (size == 0)
                throw new AppException("FILE_EMPTY");
            spool.Position = 0;
            var magic = new byte[8];
            var n = await spool.ReadAsync(magic, ct);
            var type = FileValidator.Detect(magic.AsSpan(0, n), fileName, contentType);
            spool.Position = 0;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(spool, ct)).ToLowerInvariant();
            var existing = await repository.FindHashAsync(hash, ct);
            if (existing is not null)
            {
                if (existing.Status is "Deleting" or "Deleted")
                    throw new AppException("DOCUMENT_DELETED", 409);
                return new(existing.Id, true);
            }
            var doc = new Document { FileName = Path.GetFileName(fileName.Replace('\\', '/')), ContentType = type, Sha256 = hash, SizeBytes = size };
            if (doc.FileName.Length > 240)
                throw new AppException("FILE_NAME_TOO_LONG");
            doc.ObjectKey = $"originals/{doc.Id:N}/{hash}";
            spool.Position = 0;
            await storage.EnsureBucketAsync(ct);
            await storage.PutAsync(doc.ObjectKey, spool, type, ct);
            try
            {
                await repository.AddAsync(doc, ct);
            }
            catch { await storage.DeleteAsync(doc.ObjectKey, CancellationToken.None); var winner = await repository.FindHashAsync(hash, ct); if (winner is not null) return new(winner.Id, true); throw; }
            return new(doc.Id, false);
        }
        finally { File.Delete(temp); }
    }
    public Task<Document> GetAsync(Guid id, CancellationToken ct) => repository.GetAsync(id, ct);
    public Task<IReadOnlyList<Document>> ListAsync(CancellationToken ct) => repository.ListAsync(ct);
    public Task<Guid> DeleteAsync(Guid id, CancellationToken ct) => repository.DeleteAsync(id, ct);
    public Task RetryAsync(Guid id, CancellationToken ct) => repository.RetryAsync(id, ct);
    public async Task<(Document Document, Stream Content)> OpenAsync(Guid id, CancellationToken ct)
    {
        var d = await repository.GetAsync(id, ct);
        return (d, await storage.OpenAsync(d.ObjectKey, ct));
    }
}

using System.Security.Cryptography;
using DocuAI.Application.Abstractions;
using DocuAI.Application.Exceptions;
using DocuAI.Application.Models;
using DocuAI.Domain.DTOs;
using DocuAI.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace DocuAI.Application.Services;

public sealed class DocumentService(IDocumentRepository documents, IObjectStore objectStore)
{
    private const long MaxFileSizeBytes = 50L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".pdf", ".docx", ".jpg", ".jpeg", ".png", ".wav", ".mp3", ".mp4"
    };

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await documents.ListAsync(cancellationToken);
        return items.Select(ToDto).ToArray();
    }

    public async Task<DocumentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public async Task<DocumentUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new DocumentValidationException("Choose a non-empty file.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            throw new DocumentTooLargeException();
        }

        var fileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new DocumentValidationException("This file type is not supported.");
        }

        await using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var existing = await documents.FindByHashAsync(hash, cancellationToken);
        if (existing is not null)
        {
            return new DocumentUploadResult(ToDto(existing), true);
        }

        var id = Guid.NewGuid();
        var contentType = GetContentType(extension);
        var objectKey = $"originals/{id}/{hash}{extension.ToLowerInvariant()}";
        await objectStore.EnsureBucketAsync(cancellationToken);
        await using (var uploadStream = new MemoryStream(bytes))
        {
            await objectStore.PutAsync(objectKey, uploadStream, contentType, cancellationToken);
        }

        var document = new Document
        {
            Id = id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = bytes.Length,
            Sha256 = hash,
            ObjectKey = objectKey,
            Status = DocumentStatus.Uploaded
        };

        await documents.AddAsync(document, new IngestionJob { DocumentId = id }, cancellationToken);
        return new DocumentUploadResult(ToDto(document), false);
    }

    public async Task<DocumentContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken);
        if (document is null || document.Status == DocumentStatus.Deleting)
        {
            return null;
        }

        return new DocumentContent(
            await objectStore.OpenReadAsync(document.ObjectKey, cancellationToken),
            document.ContentType,
            document.FileName);
    }

    public async Task<bool> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken);
        if (document is null)
        {
            return false;
        }

        if (document.Status != DocumentStatus.Failed)
        {
            throw new DocumentConflictException("Only failed documents can be retried.");
        }

        document.Status = DocumentStatus.Uploaded;
        document.ErrorCode = null;
        await documents.RetryAsync(document, new IngestionJob { DocumentId = document.Id }, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await documents.GetAsync(id, cancellationToken);
        if (document is null) return false;
        await objectStore.DeleteAsync(document.ObjectKey, cancellationToken);
        return await documents.DeleteAsync(id, cancellationToken);
    }

    private static DocumentDto ToDto(Document document) => new(
        document.Id,
        document.FileName,
        document.ContentType,
        document.SizeBytes,
        document.Status.ToString(),
        document.ErrorCode,
        document.CreatedAtUtc);

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".md" or ".markdown" => "text/markdown",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        _ => "application/octet-stream"
    };
}


using ProcureAI.Domain.Entities;

namespace ProcureAI.Domain.DTOs;

public sealed record DocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes,
    string State, string? ErrorCode, int ActiveGeneration, DateTimeOffset CreatedAtUtc)
{
    public static DocumentDto From(Document document) => new(document.Id, document.FileName, document.ContentType,
        document.SizeBytes, document.State, document.ErrorCode, document.ActiveGeneration, document.CreatedAtUtc);
}

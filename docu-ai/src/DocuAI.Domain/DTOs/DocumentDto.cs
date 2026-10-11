namespace DocuAI.Domain.DTOs;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    string? ErrorCode,
    DateTimeOffset CreatedAtUtc);

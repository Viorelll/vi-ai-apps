namespace SupportAI.Domain.DTOs;

public sealed record ExtractionResult(string Status, string? OperationUrl, IReadOnlyList<ExtractionSource> Sources, string? RawJson = null);

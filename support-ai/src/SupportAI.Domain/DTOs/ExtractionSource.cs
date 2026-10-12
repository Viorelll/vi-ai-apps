namespace SupportAI.Domain.DTOs;

public sealed record ExtractionSource(string Text, int? PageNumber, long? StartMs, long? EndMs);

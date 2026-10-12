namespace ProcureAI.Domain.DTOs;

public sealed record ExtractionResult(string Status, string? ProviderOperation, string? Text);

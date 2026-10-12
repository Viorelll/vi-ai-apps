namespace ProcureAI.Domain.DTOs;

public sealed record ExtractionRequest(Guid DocumentId, string ObjectKey, string FileName, string? ProviderOperation = null);

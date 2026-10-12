namespace SupportAI.Domain.DTOs;

public sealed record ExtractionRequest(Guid DocumentId, string ObjectKey, string ContentType, string FileName);

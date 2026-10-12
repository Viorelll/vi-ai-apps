namespace InspectAI.Domain.DTOs;

public sealed record ExtractionStart(Guid DocumentId, string ObjectKey, string ContentType);

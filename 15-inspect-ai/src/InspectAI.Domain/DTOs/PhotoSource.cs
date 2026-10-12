namespace InspectAI.Domain.DTOs;

public sealed record PhotoSource(
    Guid DocumentId,
    string ObjectKey,
    string ContentType,
    string Sha256
);
